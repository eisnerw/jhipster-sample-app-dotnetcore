using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using System.Text.RegularExpressions;

namespace JhipsterSampleApplication.Domain.Services
{
    public class RulesetConversionService : IRulesetConversionService
    {
        private readonly IElasticClient _elasticClient;

        public RulesetConversionService(IElasticClient elasticClient)
        {
            _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        }

        public async Task<List<string>> GetUniqueFieldValuesAsync(string indexName, string field)
        {
            var result = await _elasticClient.SearchAsync<Aggregation>(q => q
                .Size(0).Index(indexName).Aggregations(agg => agg.Terms(
                    "distinct", e => e.Field(field).Size(10000)
                ))
            );
            List<string> ret = new List<string>();
            if (result.Aggregations != null && result.Aggregations.Any())
            {
                var firstAggregation = result.Aggregations.First();
                if (firstAggregation.Value is BucketAggregate bucketAggregate && bucketAggregate.Items != null)
                {
                    foreach (var item in bucketAggregate.Items)
                    {
                        if (item is KeyedBucket<object> kb)
                        {
                            string value = kb.KeyAsString ?? kb.Key?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(value))
                            {
                                ret.Add(value);
                            }
                        }
                    }
                }
            }
            return ret;
        }

        public async Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr, string indexName, Func<string, string>? fieldMapper = null, IEnumerable<string>? documentFields = null)
        {
            fieldMapper ??= (f => f);
            documentFields ??= new[] { "wikipedia", "fname", "lname", "categories", "sign", "id" };

            if (rr.rules == null || rr.rules.Count == 0)
            {
                if (rr.@operator?.Contains("!") == true ||
                    (rr.@operator == "exists" && rr.value is bool b && !b))
                {
                    var inner = await ConvertRulesetToElasticSearch(new Ruleset
                    {
                        field = rr.field,
                        @operator = rr.@operator?.Replace("!", string.Empty),
                        value = rr.@operator == "exists" ? true : rr.value
                    }, indexName, fieldMapper, documentFields);
                    return new JObject{{
                        "bool", new JObject{{
                            "must_not", inner
                        }}
                    }};
                }

                JObject ret = new JObject{{
                    "term", new JObject{{
                        "BOGUSFIELD", "CANTMATCH"
                    }}
                }};
                if (rr.@operator?.Contains("contains") == true || rr.@operator?.Contains("like") == true)
                {
                    string stringValue = rr.value?.ToString() ?? string.Empty;
                    if (stringValue.StartsWith("/") && (stringValue.EndsWith("/") || stringValue.EndsWith("/i")))
                    {
                        bool caseInsensitive = stringValue.EndsWith("/i");
                        string re = stringValue.Substring(1, stringValue.Length - (caseInsensitive ? 3 : 2));
                        string regex = ToElasticRegEx(re.Replace(@"\\", @"\"), caseInsensitive);
                        if (regex.StartsWith("^"))
                        {
                            regex = regex.Substring(1);
                        }
                        else
                        {
                            regex = ".*" + regex;
                        }
                        if (regex.EndsWith("$"))
                        {
                            regex = regex.Substring(0, regex.Length - 1);
                        }
                        else
                        {
                            regex += ".*";
                        }
                        if (rr.field == "document")
                        {
                            List<JObject> lstRegexes = documentFields.Select(s =>
                                new JObject{{
                                    "regexp", new JObject{{
                                        s + ".keyword", new JObject{
                                            { "value", regex },
                                            { "flags", "ALL" },
        { "rewrite", "constant_score" }
                                        }
                                    }}
                                }}).ToList();
                            return new JObject{{
                                "bool", new JObject{{
                                    "should", JArray.FromObject(lstRegexes)
                                }}
                            }};
                        }
                        string mapped = fieldMapper(rr.field!);
                        return new JObject{{
                            "regexp", new JObject{{
                                mapped + ".keyword", new JObject{
                                    { "value", regex },
                                    { "flags", "ALL" },
                                    { "rewrite", "constant_score" }
                                }
                            }}
                        }};
                    }
                    string quote = Regex.IsMatch(stringValue, @"\W") ? "\"" : string.Empty;
                    string mappedField = fieldMapper(rr.field!);
                    ret = new JObject{{
                        "query_string", new JObject{{
                            "query", (rr.field != "document" ? (mappedField + ":") : string.Empty) + quote + stringValue.ToLower().Replace("\"", "\\\"") + quote
                        }}
                    }};
                }
                else if (rr.field == "dob")
                {
                    string mappedField = fieldMapper(rr.field!);
                    ret = BuildDobQuery(rr, mappedField);
                }
                else if (rr.@operator == ">" || rr.@operator == "<" || rr.@operator == ">=" || rr.@operator == "<=")
                {
                    var rangeOperator = rr.@operator switch
                    {
                        ">" => "gt",
                        ">=" => "gte",
                        "<" => "lt",
                        "<=" => "lte",
                        _ => string.Empty
                    };

                    var valueString = rr.value?.ToString() ?? string.Empty;
                    if (DateTime.TryParse(valueString, out var dateValue))
                    {
                        valueString = dateValue.ToString("yyyy-MM-dd'T'HH:mm:ss");
                    }
                    string mappedField = fieldMapper(rr.field!);
                    ret = new JObject
                    {
                        {
                            "range",
                            new JObject
                            {
                                {
                                    mappedField,
                                    new JObject { { rangeOperator, valueString } }
                                }
                            }
                        }
                    };
                }
                else if (rr.@operator?.Contains("=") == true)
                {
                    var valueStr = rr.value as string ?? string.Empty;
                    if (valueStr == "true" || valueStr == "false")
                    {
                        string mappedField = fieldMapper(rr.field!);
                        var mappingResponse = await _elasticClient.Indices.GetMappingAsync(new GetMappingRequest(indexName));
                        bool isBoolean = mappingResponse.IsValid && mappingResponse.Indices
                            .SelectMany(i => i.Value.Mappings.Properties)
                            .Any(p => p.Key == mappedField && p.Value.Type == "boolean");
                        if (isBoolean)
                        {
                            return new JObject{{
                                "term", new JObject{{
                                    mappedField, valueStr == "true"
                                }}
                            }};
                        }
                    }
                    string mapped = fieldMapper(rr.field!);
                    List<string> uniqueValues = await GetUniqueFieldValuesAsync(indexName, mapped + ".keyword");
                    List<JObject> oredTerms = uniqueValues.Where(v => v.ToLower() == (rr.value?.ToString() ?? string.Empty).ToLower()).Select(s =>
                    {
                        var termObject = new JObject();
                        termObject.Add(mapped + ".keyword", JToken.FromObject(s));
                        var result = new JObject();
                        result.Add("term", termObject);
                        return result;
                    }).ToList();
                    if (oredTerms.Count > 1)
                    {
                        ret = new JObject{{
                            "bool", new JObject{{
                                "should", JArray.FromObject(oredTerms)
                            }}
                        }};
                    }
                    else if (oredTerms.Count == 1)
                    {
                        ret = oredTerms[0];
                    }
                }
                else if (rr.@operator?.Contains("in") == true)
                {
                    var valueArray = rr.value as JArray;
                    if (valueArray is not JArray)
                    {
                        valueArray = JArray.FromObject(rr.value!);
                    }
                    if (valueArray == null || valueArray.Count == 0)
                    {
                        return new JObject{{
                            "match_none", new JObject{}
                        }};
                    }
                    string mapped = fieldMapper(rr.field!);
                    List<string> uniqueValues = await GetUniqueFieldValuesAsync(indexName, mapped + ".keyword");
                    var lowered = uniqueValues.Select(s => new { raw = s, lower = s.ToLower() }).ToList();
                    var requested = valueArray
                        .Select(v => (v?.ToString() ?? string.Empty).ToLower())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                    var matched = lowered
                        .Where(u => requested.Contains(u.lower))
                        .Select(u => u.raw)
                        .Distinct()
                        .ToList();
                    if (matched.Count == 0)
                    {
                        return new JObject{{
                            "match_none", new JObject{}
                        }};
                    }
                    return new JObject{{
                        "terms", new JObject{{
                            mapped + ".keyword", JArray.FromObject(matched)
                        }}
                    }};
                }
                else if (rr.@operator?.Contains("exists") == true)
                {
                    string mapped = fieldMapper(rr.field!);
                    List<JObject> lstExists = new List<JObject>();
                    List<JObject> lstEmptyString = new List<JObject>{
                        new JObject{{
                            "term", new JObject{{
                                mapped + ".keyword", ""
                            }}
                        }}
                    };
                    lstExists.Add(new JObject{{
                        "exists", new JObject{{
                            "field", mapped
                        }}
                    }});
                    lstExists.Add(new JObject{{
                        "bool", new JObject{{
                            "must_not", JArray.FromObject(lstEmptyString)
                        }}
                    }});
                    ret = new JObject{{
                        "bool", new JObject{{
                            "must", JArray.FromObject(lstExists)
                        }}
                    }};
                }
                return ret;
            }
            else
            {
                List<object> rls = new List<object>();
                for (int i = 0; i < rr.rules.Count; i++)
                {
                    rls.Add(await ConvertRulesetToElasticSearch(rr.rules[i], indexName, fieldMapper, documentFields));
                }
                if (rr.condition == "and")
                {
                    return new JObject{{
                        "bool", new JObject{{
                            rr.not == true ? "must_not" : "must", JArray.FromObject(rls)
                        }}
                    }};
                }
                JObject ret = new JObject{{
                    "bool", new JObject{{
                        "should", JArray.FromObject(rls)
                    }}
                }};
                if (rr.not == true)
                {
                    ret = new JObject{{
                        "bool", new JObject{{
                            "must_not", JObject.FromObject(ret)
                        }}
                    }};
                }
                return ret;
            }
        }

        private static (DateTime start, DateTime? endExclusive) ParseDateValue(string value)
        {
            if (Regex.IsMatch(value, @"^\d{4}$"))
            {
                var year = int.Parse(value);
                var start = new DateTime(year, 1, 1, 0, 0, 0);
                return (start, start.AddYears(1));
            }
            if (Regex.IsMatch(value, @"^\d{4}-\d{2}$"))
            {
                var year = int.Parse(value.Substring(0, 4));
                var month = int.Parse(value.Substring(5, 2));
                var start = new DateTime(year, month, 1, 0, 0, 0);
                return (start, start.AddMonths(1));
            }
            if (Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}$"))
            {
                var year = int.Parse(value.Substring(0, 4));
                var month = int.Parse(value.Substring(5, 2));
                var day = int.Parse(value.Substring(8, 2));
                var start = new DateTime(year, month, day, 0, 0, 0);
                return (start, start.AddDays(1));
            }
            if (DateTime.TryParse(value, out var dt))
            {
                return (dt, null);
            }
            return (DateTime.MinValue, null);
        }

        private static JObject BuildDobQuery(Ruleset rr, string field)
        {
            var valueString = rr.value?.ToString() ?? string.Empty;
            var (start, endExclusive) = ParseDateValue(valueString);
            var startStr = start.ToString("yyyy-MM-dd'T'HH:mm:ss");
            JObject rangeBody = new JObject();

            switch (rr.@operator)
            {
                case ">":
                    if (endExclusive.HasValue)
                        rangeBody.Add("gte", endExclusive.Value.ToString("yyyy-MM-dd'T'HH:mm:ss"));
                    else
                        rangeBody.Add("gt", startStr);
                    break;
                case ">=":
                    rangeBody.Add("gte", startStr);
                    break;
                case "<":
                    rangeBody.Add("lt", startStr);
                    break;
                case "<=":
                    if (endExclusive.HasValue)
                        rangeBody.Add("lt", endExclusive.Value.ToString("yyyy-MM-dd'T'HH:mm:ss"));
                    else
                        rangeBody.Add("lte", startStr);
                    break;
                case "=":
                case "!=":
                    if (endExclusive.HasValue)
                    {
                        rangeBody.Add("gte", startStr);
                        rangeBody.Add("lt", endExclusive.Value.ToString("yyyy-MM-dd'T'HH:mm:ss"));
                    }
                    else
                    {
                        rangeBody.Add("gte", startStr);
                        rangeBody.Add("lte", startStr);
                    }
                    break;
            }

            return new JObject
            {
                { "range", new JObject { { field, rangeBody } } }
            };
        }

        private static string ToElasticRegEx(string pattern, bool caseInsensitive)
        {
            string ret = "";
            string[] regexTokens = Regex.Replace(pattern, @"([\[\]]|\\\\|\\\[|\\\]|\\s|\\S|\\w|\\W|\\d|\\D|.)", "`$1").Split('`');
            bool bracketed = false;
            for (int i = 1; i < regexTokens.Length; i++)
            {
                if (bracketed)
                {
                    switch (regexTokens[i])
                    {
                        case "]":
                            bracketed = false;
                            ret += regexTokens[i];
                            break;
                        case @"\s":
                            ret += " \n\t\r";
                            break;
                        case @"\d":
                            ret += "0-9";
                            break;
                        case @"\w":
                            ret += "A-Za-z_";
                            break;
                        default:
                            if (caseInsensitive && Regex.IsMatch(regexTokens[i], @"^[A-Za-z]+$"))
                            {
                                if ((i + 2) < regexTokens.Length && regexTokens[i + 1] == "-" && Regex.IsMatch(regexTokens[i + 2], @"^[A-Za-z]+$"))
                                {
                                    ret += regexTokens[i].ToLower() + "-" + regexTokens[i + 2].ToLower() + regexTokens[i].ToUpper() + "-" + regexTokens[i + 2].ToUpper();
                                    i += 2;
                                }
                                else
                                {
                                    ret += regexTokens[i].ToLower() + regexTokens[i].ToUpper();
                                }
                            }
                            else
                            {
                                ret += regexTokens[i];
                            }
                            break;
                    }
                }
                else if (regexTokens[i] == "[")
                {
                    bracketed = true;
                    ret += regexTokens[i];
                }
                else if (regexTokens[i] == @"\s")
                {
                    ret += @"[ \n\t\r]";
                }
                else if (regexTokens[i] == @"\d")
                {
                    ret += @"[0-9]";
                }
                else if (regexTokens[i] == @"\w")
                {
                    ret += @"[A-Za-z_]";
                }
                else if (caseInsensitive && Regex.IsMatch(regexTokens[i], @"[A-Za-z]"))
                {
                    ret += "[" + regexTokens[i].ToLower() + regexTokens[i].ToUpper() + "]";
                }
                else
                {
                    ret += regexTokens[i];
                }
            }
            return ret;
        }
    }
}
