using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace JhipsterSampleApplication.Domain.Services
{
    public class BqlService<TDomain> : IBqlService<TDomain> where TDomain : class
    {
        private readonly ILogger<BqlService<TDomain>> _logger;
        private readonly INamedQueryService _namedQueryService;
        private readonly JObject _qbSpec;
        private readonly string _domain;

        // Computed spec data
        private readonly HashSet<string> _validFields = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _fieldTypeByName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _fieldOperatorsLowerByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _operatorMapLowerByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _allowedBqlTokensUpperByField = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _categoryAllowedValuesByField = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> _allOperatorTokensUpper = new HashSet<string>(StringComparer.Ordinal);
        // Case-insensitive keyword support per field (e.g., country.ci)
        private readonly Dictionary<string, string> _ciFieldByName = new Dictionary<string, string>(StringComparer.Ordinal);
        private string? _defaultFullTextField;
        public BqlService(ILogger<BqlService<TDomain>> logger, INamedQueryService namedQueryService, JObject qbSpec, string domain)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _namedQueryService = namedQueryService ?? throw new ArgumentNullException(nameof(namedQueryService));
            _qbSpec = qbSpec ?? throw new ArgumentNullException(nameof(qbSpec));
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));

            BuildSpecCaches();
        }

        public virtual RulesetDto Bql2Ruleset(string bqlQuery, IEnumerable<NamedQueryDto>? userSystemAndGlobalRulesets = null)
        {
            IEnumerable<string>? namedQueryNames = null;
            namedQueryNames = (userSystemAndGlobalRulesets ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n.Name))
                .Select(n => n.Name!.ToUpperInvariant())
                .Distinct();

            if (!ValidateBqlQuery(bqlQuery, namedQueryNames))
            {
                throw new ArgumentException($"Invalid BQL query: '{bqlQuery}'", nameof(bqlQuery));
            }

            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                return new RulesetDto
                {
                    condition = "or",
                    not = false,
                    rules = null
                };
            }

            MatchCollection matches = BuildTokenizer().Matches(bqlQuery);
            string[] tokens = [.. matches.Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))];

            if (tokens.Length == 0)
            {
                throw new ArgumentException("Invalid BQL query format", nameof(bqlQuery));
            }

            var normalizedTokens = new List<string>(tokens.Length);
            foreach (string token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                // Preserve quoted strings and regex patterns verbatim
                if ((token.StartsWith('/') && (token.EndsWith('/') || token.EndsWith("/i"))) || (token.StartsWith('"') && token.EndsWith('"')))
                {
                    normalizedTokens.Add(token);
                    continue;
                }
                string core = token;
                int closingCount = 0;
                while (core.Length > 0 &&
                    core.EndsWith(')') &&
                    core != ")" &&
                    !core.StartsWith('('))
                {
                    closingCount++;
                    core = core.Substring(0, core.Length - 1);
                }

                if (!string.IsNullOrWhiteSpace(core))
                {
                    normalizedTokens.Add(core);
                }

                for (int i = 0; i < closingCount; i++)
                {
                    normalizedTokens.Add(")");
                }
            }

            tokens = [..normalizedTokens];
            (bool matches, int index, RulesetDto ruleset) result = ParseRuleset(tokens, 0, false, userSystemAndGlobalRulesets);
            if (!result.matches || result.index < tokens.Length)
            {
                throw new ArgumentException($"Invalid BQL query '{bqlQuery}' at position {result.index}", nameof(bqlQuery));
            }

            return result.ruleset;
        }

        public virtual string Ruleset2Bql(RulesetDto ruleset)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            if (!ValidateRuleset(ruleset))
            {
                throw new ArgumentException("Invalid ruleset", nameof(ruleset));
            }
            if (ruleset.rules == null || ruleset.rules.Count == 0)
            {
                ruleset = new RulesetDto
                {
                    condition = "or",
                    rules = [ruleset]
                };
            }

            return QueryAsString(ruleset);
        }

        public virtual async Task<object> Ruleset2ElasticSearch(RulesetDto ruleset, IEnumerable<string>? documentKeywordFields = null)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            // The generic implementation assumes the ruleset has already been validated
            // by the calling service.  All rules share the same conversion logic, so we
            // recursively build an Elasticsearch query represented as a JObject.

            if (ruleset.rules == null || ruleset.rules.Count == 0)
            {
                // Handle negated operators (e.g. !LIKE, !IN, !EXISTS) by generating the
                // positive query and wrapping it in a must_not bool clause.
                if (ruleset.@operator?.Contains('!') == true ||
                    (ruleset.@operator == "exists" && ruleset.value is bool b && !b))
                {
                    object inner = await Ruleset2ElasticSearch(new RulesetDto
                    {
                        field = ruleset.field,
                        @operator = ruleset.@operator?.Replace("!", string.Empty),
                        value = ruleset.@operator == "exists" ? true : ruleset.value,
                        rules = null
                    }, documentKeywordFields);

                    return new JObject
                    {
                        {
                            "bool", new JObject
                            {
                                { "must_not", JToken.FromObject(inner) }
                            }
                        }
                    };
                }

                JObject ret = new()
                {
                    { "term", new JObject{ { "BOGUSFIELD", "CANTMATCH" } } }
                };
                if (ruleset.field == "daysFromToday" || ruleset.field == "minutesFromNow")
                {
                    DateTime dt = DateTime.UtcNow;
                    if (ruleset.value?.ToString() != "0")
                    {
                        int minus = int.Parse(ruleset.value?.ToString() ?? "");
                        dt = ruleset.field.StartsWith("days") ? dt.AddDays(minus) : dt.AddMinutes(minus);
                    }
                    ruleset.value = ruleset.field.StartsWith("days") ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-ddTHH:mm");
                    ruleset.field = ruleset.field.StartsWith("days") ? "date" : "datetime";
                }
                if ((ruleset.@operator?.Contains("contains", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (ruleset.@operator?.Contains("like", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    if (ruleset.value is IEnumerable<object> list && ruleset.value is not string)
                    {
                        var shouldClauses = new JArray();
                        foreach (object item in list)
                        {
                            string itemValue = item switch
                            {
                                JValue jv => jv.Value?.ToString() ?? string.Empty,
                                _ => item?.ToString() ?? string.Empty
                            };

                            if (string.IsNullOrWhiteSpace(itemValue))
                            {
                                continue;
                            }

                            RulesetDto childRule = new()
                            {
                                field = ruleset.field,
                                @operator = (ruleset.@operator?.Contains("like", StringComparison.OrdinalIgnoreCase) ?? false) ? "like" : "contains",
                                value = itemValue
                            };

                            object childQuery = await Ruleset2ElasticSearch(childRule, documentKeywordFields);
                            shouldClauses.Add(JToken.FromObject(childQuery));
                        }

                        if (shouldClauses.Count == 0)
                        {
                            return ret;
                        }

                        return new JObject
                        {
                            {
                                "bool",
                                new JObject
                                {
                                    { "should", shouldClauses },
                                    { "minimum_should_match", 1 }
                                }
                            }
                        };
                    }

                    string stringValue = ruleset.value?.ToString() ?? string.Empty;

                    // Support regex literals: /exp/ or /exp/i
                    if (stringValue.StartsWith('/') &&
                       (stringValue.EndsWith('/') || stringValue.EndsWith("/i")))
                    {
                        bool caseInsensitive = stringValue.EndsWith("/i");
                        string pattern = stringValue.Substring(1, stringValue.Length - (caseInsensitive ? 3 : 2));
                        string regex = ToElasticRegEx(pattern.Replace(@"\\", @"\"), caseInsensitive);

                        if (!regex.StartsWith('^'))
                        {
                            regex = ".*" + regex;
                        }
                        else
                        {
                            regex = regex.Substring(1);
                        }

                        if (!regex.EndsWith('$'))
                        {
                            regex += ".*";
                        }
                        else
                        {
                            regex = regex.Substring(0, regex.Length - 1);
                        }

                        if (string.Equals(ruleset.field, "document", StringComparison.OrdinalIgnoreCase))
                        {
                            List<string> keywordFields = GetDocumentKeywordFields(documentKeywordFields);
                            if (keywordFields.Count == 0)
                            {
                                return ret;
                            }

                            if (keywordFields.Count == 1)
                            {
                                return BuildRegexpQuery(keywordFields[0] + ".keyword", regex);
                            }

                            return new JObject
                            {
                                {
                                    "bool",
                                    new JObject
                                    {
                                        {
                                            "should",
                                            new JArray(keywordFields.Select(field => BuildRegexpQuery(field + ".keyword", regex)))
                                        },
                                        { "minimum_should_match", 1 }
                                    }
                                }
                            };
                        }

                        return BuildRegexpQuery(ruleset.field + ".keyword", regex);
                    }

                    string quote = Regex.IsMatch(stringValue, @"\W") ? "\"" : string.Empty;
                    ret = new JObject
                    {
                        {
                            "query_string",
                            new JObject
                            {
                                { "query", (ruleset.field != "document" ? (ruleset.field + ":") : string.Empty) +
                                            quote + stringValue.ToLower().Replace("\"", "\\\"") + quote }
                            }
                        }
                    };
                }
                else if (ruleset.@operator == ">" || ruleset.@operator == ">=" ||
                         ruleset.@operator == "<" || ruleset.@operator == "<=")
                {
                    string valueString = ruleset.value?.ToString() ?? string.Empty;

                    if (Regex.IsMatch(valueString, @"^\d{4}(?:-\d{2}(?:-\d{2})?)?$") ||
                        DateTime.TryParse(valueString, out _))
                    {
                        return BuildDateQuery(ruleset.field!, ruleset.@operator!, valueString);
                    }

                    string rangeOperator = ruleset.@operator switch
                    {
                        ">" => "gt",
                        ">=" => "gte",
                        "<" => "lt",
                        "<=" => "lte",
                        _ => string.Empty
                    };

                    return new JObject
                    {
                        {
                            "range",
                            new JObject
                            {
                                {
                                    ruleset.field!,
                                    new JObject { { rangeOperator, valueString } }
                                }
                            }
                        }
                    };
                }
                else if (ruleset.@operator?.Contains('=') == true)
                {
                    string valueStr = ruleset.value?.ToString() ?? string.Empty;

                    // Boolean fields: use exact term query with boolean value
                    if (!string.IsNullOrWhiteSpace(ruleset.field) &&
                        _fieldTypeByName.TryGetValue(ruleset.field!, out string? ftype) &&
                        string.Equals(ftype, "boolean", StringComparison.OrdinalIgnoreCase))
                    {
                        bool boolVal = string.Equals(valueStr, "true", StringComparison.OrdinalIgnoreCase);
                        return new JObject
                        {
                            { "term", new JObject { { ruleset.field!, boolVal } } }
                        };
                    }

                    if (Regex.IsMatch(valueStr, @"^\d{4}(?:-\d{2}(?:-\d{2})?)?$") ||
                        DateTime.TryParse(valueStr, out _))
                    {
                        return BuildDateQuery(ruleset.field!, "=", valueStr);
                    }

                    string valueLower = valueStr.ToLowerInvariant();

                    if (TryGetCiField(ruleset.field, out var ciField))
                    {
                        // Fast path: exact case-insensitive keyword
                        ret = new JObject
                        {
                            { "term", new JObject { { ciField!, valueLower } } }
                        };
                    }
                    else
                    {
                        // Fallback: analyzer prefilter + exact script compare
                        ret = new JObject
                        {
                            {
                                "bool",
                                new JObject
                                {
                                    {
                                        "must",
                                        new JArray
                                        {
                                            new JObject
                                            {
                                                {
                                                    "match",
                                                    new JObject
                                                    {
                                                        {
                                                            ruleset.field!,
                                                            new JObject
                                                            {
                                                                { "query", valueLower },
                                                                { "operator", "and" }
                                                            }
                                                        }
                                                    }
                                                }
                                            },
                                            new JObject
                                            {
                                                {
                                                    "script",
                                                    new JObject
                                                    {
                                                        {
                                                            "script",
                                                            new JObject
                                                            {
                                                                { "source", $"def f = doc['{ruleset.field}.keyword']; if (f.size()==0) return false; for (def v : f) {{ if (v != null && v.toLowerCase() == params.query) return true; }} return false;" },
                                                                { "params", new JObject { { "query", valueLower } } }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        };
                    }
                }
                else if (ruleset.@operator?.Contains("in") == true)
                {
                    var valueArray = ruleset.value as IEnumerable<object>;
                    List<string> values = valueArray?.Select(v => v?.ToString() ?? string.Empty)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList() ?? [];

                    // Boolean fields: terms with booleans
                    if (!string.IsNullOrWhiteSpace(ruleset.field) &&
                        _fieldTypeByName.TryGetValue(ruleset.field!, out var ftype2) &&
                        string.Equals(ftype2, "boolean", StringComparison.OrdinalIgnoreCase))
                    {
                        var bools = values.Select(s => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)).ToList();
                        return new JObject
                        {
                            { "terms", new JObject { { ruleset.field!, JArray.FromObject(bools) } } }
                        };
                    }

                    // Prefer fast CI subfield if available; otherwise fallback to match+script
                    if (TryGetCiField(ruleset.field, out var ciField))
                    {
                        var lowerValues = values.Select(s => s.ToLowerInvariant()).ToList();
                        ret = new JObject
                        {
                            { "terms", new JObject { { ciField!, JArray.FromObject(lowerValues) } } }
                        };
                    }
                    else
                    {
                        // Case-insensitive IN: analyzer prefilter OR + exact check via script
                        var lowerValues = values.Select(s => s.ToLowerInvariant()).ToList();
                        string matchQuery = string.Join(" ", lowerValues);
                        ret = new JObject
                        {
                            {
                                "bool",
                                new JObject
                                {
                                    {
                                        "must",
                                        new JArray
                                        {
                                            new JObject
                                            {
                                                {
                                                    "match",
                                                    new JObject
                                                    {
                                                        {
                                                            ruleset.field!,
                                                            new JObject
                                                            {
                                                                { "query", matchQuery },
                                                                { "operator", "or" }
                                                            }
                                                        }
                                                    }
                                                }
                                            },
                                            new JObject
                                            {
                                                {
                                                    "script",
                                                    new JObject
                                                    {
                                                        {
                                                            "script",
                                                            new JObject
                                                            {
                                                                { "source", $"def f = doc['{ruleset.field}.keyword']; if (f.size()==0) return false; for (def v : f) {{ if (v != null && params.values.contains(v.toLowerCase())) return true; }} return false;" },
                                                                { "params", new JObject { { "values", JArray.FromObject(lowerValues) } } }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        };
                    }
                }
                else if (ruleset.@operator?.Contains("exists") == true)
                {
                    ret = new JObject
                    {
                        { "exists", new JObject{ { "field", ruleset.field } } }
                    };
                }

                return ret;
            }
            else
            {
                var rls = new List<object>();
                foreach (RulesetDto rule in ruleset.rules!)
                {
                    rls.Add(await Ruleset2ElasticSearch(rule, documentKeywordFields));
                }

                if (ruleset.condition == "and")
                {
                    return new JObject
                    {
                        {
                            "bool", new JObject
                            {
                                { ruleset.not ? "must_not" : "must", JArray.FromObject(rls) }
                            }
                        }
                    };
                }

                JObject ret = new()
                {
                    { "bool", new JObject { { "should", JArray.FromObject(rls) } } }
                };

                if (ruleset.not)
                {
                    ret = new JObject
                    {
                        { "bool", new JObject { { "must_not", JObject.FromObject(ret) } } }
                    };
                }

                return ret;
            }
        }

        private static JObject BuildRegexpQuery(string field, string regex)
        {
            return new JObject
            {
                {
                    "regexp",
                    new JObject
                    {
                        {
                            field,
                            new JObject
                            {
                                { "value", regex },
                                { "flags", "ALL" },
                                { "rewrite", "constant_score" }
                            }
                        }
                    }
                }
            };
        }

        private List<string> GetDocumentKeywordFields(IEnumerable<string>? documentKeywordFields)
        {
            List<string> explicitFields = (documentKeywordFields ?? Enumerable.Empty<string>())
                .Select(NormalizeKeywordFieldName)
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (explicitFields.Count > 0)
            {
                return explicitFields;
            }

            return _validFields
                .Where(field =>
                    !string.Equals(field, "document", StringComparison.OrdinalIgnoreCase) &&
                    _fieldTypeByName.TryGetValue(field, out string? type) &&
                    string.Equals(type, "string", StringComparison.OrdinalIgnoreCase) &&
                    _allowedBqlTokensUpperByField.TryGetValue(field, out HashSet<string>? allowedOps) &&
                    (allowedOps.Contains("CONTAINS") || allowedOps.Contains("LIKE")))
                .ToList();
        }

        private static string NormalizeKeywordFieldName(string? field)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return string.Empty;
            }

            string normalized = field.Trim();
            if (normalized.EndsWith(".keyword", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - ".keyword".Length);
            }

            return string.Equals(normalized, "document", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalized;
        }

        private static (DateTime start, DateTime? endExclusive) ParseDateValue(string value)
        {
            if (Regex.IsMatch(value, @"^\d{4}$"))
            {
                int year = int.Parse(value);
                var start = new DateTime(year, 1, 1, 0, 0, 0);
                return (start, start.AddYears(1));
            }
            if (Regex.IsMatch(value, @"^\d{4}-\d{2}$"))
            {
                int year = int.Parse(value.Substring(0, 4));
                int month = int.Parse(value.Substring(5, 2));
                var start = new DateTime(year, month, 1, 0, 0, 0);
                return (start, start.AddMonths(1));
            }
            if (Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}$"))
            {
                int year = int.Parse(value.Substring(0, 4));
                int month = int.Parse(value.Substring(5, 2));
                int day = int.Parse(value.Substring(8, 2));
                DateTime start = new (year, month, day, 0, 0, 0);
                return (start, start.AddDays(1));
            }
            // yyyy-MM-ddTHH:mm
            if (Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$"))
            {
                int year = int.Parse(value.Substring(0, 4));
                int month = int.Parse(value.Substring(5, 2));
                int day = int.Parse(value.Substring(8, 2));
                int hour = int.Parse(value.Substring(11, 2));
                int minute = int.Parse(value.Substring(14, 2));
                DateTime start = new(year, month, day, hour, minute, 0);
                return (start, null);
            }
            if (DateTime.TryParse(value, out DateTime dt))
            {
                return (dt, null);
            }
            return (DateTime.MinValue, null);
        }
        private static JObject BuildDateQuery(string field, string op, string value)
        {
            (DateTime start, DateTime? endExclusive) = ParseDateValue(value);
            string startStr = start.ToString("yyyy-MM-dd'T'HH:mm:ss");

            JObject rangeBody = [];

            switch (op)
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

       protected virtual bool ValidateRuleset(RulesetDto ruleset)
        {
            if (ruleset == null)
            {
                _logger.LogWarning("Null Ruleset provided");
                return false;
            }
            return true;
        }

        private (bool matches, int index, RulesetDto ruleset) ParseRuleset(string[] tokens, int index, bool not, IEnumerable<NamedQueryDto>? userSystemAndGlobalRulesets)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            (bool matches, int index, RulesetDto ruleset) result = ParseAndOrRuleset(tokens, index, not, userSystemAndGlobalRulesets);
            if (!result.matches)
            {
                result = ParseNotRuleset(tokens, index, userSystemAndGlobalRulesets);
                if (!result.matches)
                {
                    result = ParseParened(tokens, index, not, userSystemAndGlobalRulesets);
                }
            }

            return result;
        }

        private (bool matches, int index, RulesetDto ruleset) ParseAndOrRuleset(string[] tokens, int index, bool not, IEnumerable<NamedQueryDto>? userSystemAndGlobalRulesets)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            var rules = new List<RulesetDto>();
            (bool matches, int index, RulesetDto ruleset) result = ParseParened(tokens, index, not, userSystemAndGlobalRulesets);
            if (!result.matches)
            {
                result = ParseRule(tokens, index);
                if (!result.matches)
                {
                    return (false, index, new RulesetDto());
                }
            }

            if (result.index >= tokens.Length || (tokens[result.index] != "&" && tokens[result.index] != "|"))
            {
                return result;
            }

            string condition = tokens[result.index];
            rules.Add(result.ruleset);
            index = result.index + 1;

            while (index < tokens.Length)
            {
                result = ParseParened(tokens, index, not, userSystemAndGlobalRulesets);
                if (!result.matches)
                {
                    result = ParseRule(tokens, index);
                    if (!result.matches)
                    {
                        break;
                    }
                }

                if (result.matches)
                {
                    rules.Add(result.ruleset);
                    index = result.index;
                    if (index >= tokens.Length || tokens[index] != condition)
                    {
                        break;
                    }
                    index++;
                }
                else
                {
                    break;
                }
            }

            if (rules.Count < 2)
            {
                return (false, index, new RulesetDto());
            }

            return (true, index, new RulesetDto
            {
                condition = condition == "&" ? "and" : "or",
                rules = rules,
                not = not
            });
        }

        private (bool matches, int index, RulesetDto ruleset) ParseNotRuleset(string[] tokens, int index, IEnumerable<NamedQueryDto>? userSystemAndGlobalRulesets)
        {
            if (index >= tokens.Length || tokens[index] != "!")
            {
                return (false, index, new RulesetDto());
            }

            index++;
            (bool matches, int index, RulesetDto ruleset) result = ParseParened(tokens, index, true, userSystemAndGlobalRulesets);
            if (!result.matches)
            {
                return (false, index, new RulesetDto());
            }

            return result;
        }

        private (bool matches, int index, RulesetDto ruleset) ParseParened(string[] tokens, int index, bool not, IEnumerable<NamedQueryDto>? userSystemAndGlobalRulesets)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            bool negate = not;
            while (index < tokens.Length && tokens[index] == "!")
            {
                negate = !negate;
                index++;
            }

            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            // Named ruleset reference (UPPER_SNAKE_CASE)
            if (Regex.IsMatch(tokens[index], @"^(?=.*[A-Z])[A-Z0-9_]+$"))
            {
                string rulesetName = tokens[index];
                NamedQuery? namedQuery = _namedQueryService.FindByNameAndOwner(rulesetName, null, _domain).GetAwaiter().GetResult();
                string? namedQueryText = namedQuery?.Text;

                if (!string.IsNullOrWhiteSpace(namedQueryText))
                {
                    RulesetDto namedRuleset = Bql2Ruleset(namedQueryText, userSystemAndGlobalRulesets);
                    namedRuleset.name = rulesetName;
                    RulesetDto adjusted = negate ? ApplyNegation(namedRuleset) : namedRuleset;
                    return (true, index + 1, adjusted);
                }
                return (false, index, new RulesetDto());
            }

            // Check for parenthesized expression
            if (tokens[index] != "(")
            {
                return (false, index, new RulesetDto());
            }

            index++;
            (bool matches, int index, RulesetDto ruleset) result = ParseRuleset(tokens, index, false, userSystemAndGlobalRulesets);
            if (!result.matches)
            {
                result = ParseRule(tokens, index);
                if (!result.matches)
                {
                    return result;
                }
            }

            if (result.index >= tokens.Length || tokens[result.index] != ")")
            {
                return (false, result.index, new RulesetDto());
            }

            var adjustedRuleset = negate ? ApplyNegation(result.ruleset) : result.ruleset;
            return (true, result.index + 1, adjustedRuleset);
        }

        private (bool matches, int index, RulesetDto ruleset) ParseRule(string[] tokens, int index)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            // Handle default full-text field (e.g., document) with direct value
            if (!_validFields.Contains(tokens[index]))
            {
                if (_defaultFullTextField is not null &&
                    (Regex.IsMatch(tokens[index], @"^[A-Za-z0-9?*]+$") || tokens[index].StartsWith("\"") || tokens[index].StartsWith("/")))
                {
                    string docValue = tokens[index];
                    if (docValue.StartsWith('"'))
                    {
                        if (docValue.EndsWith("\\\""))
                        {
                            return (false, index, new RulesetDto());
                        }
                    }
                    else if (docValue.StartsWith('/'))
                    {
                        try
                        {
                            string pattern = docValue.Substring(1);
                            if (pattern.EndsWith("/i"))
                            {
                                pattern = pattern.Substring(0, pattern.Length - 2);
                            }
                            else if (pattern.EndsWith('/'))
                            {
                                pattern = pattern.Substring(0, pattern.Length - 1);
                            }
                            _ = new Regex(pattern);
                        }
                        catch
                        {
                            return (false, index, new RulesetDto());
                        }
                    }
                    else if (docValue.StartsWith('"')  && docValue.EndsWith('"'))
                    {
                        docValue = docValue.Substring(1, docValue.Length - 2);
                    }
                    var op = Regex.IsMatch(docValue ?? string.Empty, @"^/.*/i?$", RegexOptions.IgnoreCase) ? "like" : "contains";
                    return (true, index + 1, new RulesetDto
                    {
                        field = _defaultFullTextField,
                        @operator = op,
                        value = docValue ?? string.Empty,
                        rules = null
                    });
                }
                return (false, index, new RulesetDto());
            }

            string field = tokens[index];
            index++;

            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            string opToken = tokens[index];
            if (!IsValidOperator(field, opToken))
            {
                return (false, index, new RulesetDto());
            }
            index++;

            // EXISTS / !EXISTS
            if (opToken == "EXISTS" || opToken == "!EXISTS")
            {
                return (true, index, new RulesetDto
                {
                    field = field,
                    @operator = "exists",
                    value = !opToken.StartsWith('!')
                });
            }

            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            // IN / !IN list
            if (opToken == "IN" || opToken == "!IN")
            {
                if (!TryParseInValues(field, tokens, ref index, out List<string>? parsedValues) || parsedValues.Count == 0)
                {
                    return (false, index, new RulesetDto());
                }

                return (true, index, new RulesetDto
                {
                    field = field,
                    @operator = opToken == "IN" ? "in" : "!in",
                    value = parsedValues,
                    rules = null
                });
            }

            if ((opToken == "CONTAINS" || opToken == "!CONTAINS") &&
                index < tokens.Length &&
                tokens[index].StartsWith('('))
            {
                if (!TryParseInValues(field, tokens, ref index, out List<string>? containsValues) || containsValues.Count == 0)
                {
                    return (false, index, new RulesetDto());
                }

                return (true, index, new RulesetDto
                {
                    field = field,
                    @operator = opToken == "CONTAINS" ? "contains" : "!contains",
                    value = containsValues,
                    rules = null
                });
            }

            // Single value
            string value = tokens[index];
            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value.Substring(1, value.Length - 2);
            }

            if (!IsValidValue(field, value))
            {
                return (false, index, new RulesetDto());
            }

            // Map op token to ruleset operator, preserving negation for contains/like
            string rulesetOperator = OpTokenToRulesetOperator(opToken);

            return (true, index + 1, new RulesetDto
            {
                field = field,
                @operator = rulesetOperator,
                value = value ?? string.Empty,
                rules = null
            });
        }

        private static RulesetDto ApplyNegation(RulesetDto ruleset)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            if (!string.IsNullOrWhiteSpace(ruleset.condition) && ruleset.rules != null && ruleset.rules.Count > 0)
            {
                ruleset.not = !ruleset.not;
                return ruleset;
            }

            if (TryNegateLeafRule(ruleset))
            {
                return ruleset;
            }

            ruleset.not = false;
            return new RulesetDto
            {
                condition = "or",
                not = true,
                rules = [ruleset]
            };
        }

        private static bool TryNegateLeafRule(RulesetDto rule)
        {
            if (rule == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(rule.condition) && rule.rules != null && rule.rules.Count > 0)
            {
                return false;
            }

            string? op = rule.@operator?.ToLowerInvariant();
            switch (op)
            {
                case "=":
                    rule.@operator = "!=";
                    rule.not = false;
                    return true;
                case "!=":
                    rule.@operator = "=";
                    rule.not = false;
                    return true;
                case "contains":
                    rule.@operator = "!contains";
                    rule.not = false;
                    return true;
                case "!contains":
                    rule.@operator = "contains";
                    rule.not = false;
                    return true;
                case "like":
                    rule.@operator = "!like";
                    rule.not = false;
                    return true;
                case "!like":
                    rule.@operator = "like";
                    rule.not = false;
                    return true;
                case "in":
                    rule.@operator = "!in";
                    rule.not = false;
                    return true;
                case "!in":
                    rule.@operator = "in";
                    rule.not = false;
                    return true;
                case "exists":
                    if (rule.value is bool boolVal)
                    {
                        rule.value = !boolVal;
                        rule.not = false;
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
        private bool TryParseInValues(string field, string[] tokens, ref int index, out List<string> normalizedValues)
        {
            normalizedValues = [];
            if (index >= tokens.Length)
            {
                return false;
            }

            string combinedValuesToken;
            int nextIndex;

            if (tokens[index].StartsWith('(') && tokens[index].EndsWith(')'))
            {
                combinedValuesToken = tokens[index];
                nextIndex = index + 1;
            }
            else if (TryCollectParenthesizedTokens(tokens, index, out combinedValuesToken, out nextIndex))
            {
                // combinedValuesToken populated by helper
            }
            else
            {
                return false;
            }

            List<string> extracted = ExtractInValues(field, combinedValuesToken);
            if (extracted.Count == 0)
            {
                return false;
            }

            normalizedValues = extracted;
            index = nextIndex;
            return true;
        }
        private static bool TryCollectParenthesizedTokens(string[] tokens, int startIndex, out string combined, out int nextIndex)
        {
            combined = string.Empty;
            nextIndex = startIndex;

            if (startIndex >= tokens.Length)
            {
                return false;
            }

            var sb = new StringBuilder();
            int depth = 0;
            bool started = false;

            for (int i = startIndex; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!started)
                {
                    if (token == "(" || token.StartsWith('('))
                    {
                        started = true;
                    }
                    else
                    {
                        return false;
                    }
                }

                sb.Append(token);

                depth += CountParenthesis(token, '(');
                depth -= CountParenthesis(token, ')');

                if (started && depth <= 0)
                {
                    string candidate = sb.ToString();
                    if (candidate.StartsWith('(') && candidate.EndsWith(')'))
                    {
                        combined = candidate;
                        nextIndex = i + 1;
                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        private static int CountParenthesis(string token, char paren)
        {
            if (string.IsNullOrEmpty(token))
            {
                return 0;
            }

            int count = 0;
            foreach (char ch in token)
            {
                if (ch == paren)
                {
                    count++;
                }
            }
            return count;
        }
        private static string OpTokenToRulesetOperator(string opToken)
        {
            return opToken switch
            {
                "CONTAINS" => "contains",
                "!CONTAINS" => "!contains",
                "LIKE" => "like",
                "!LIKE" => "!like",
                _ => opToken.ToLowerInvariant(),
            };
        }
       private static string ToElasticRegEx(string pattern, bool caseInsensitive)
        {
            string ret = string.Empty;
            string[] tokens = Regex.Replace(pattern, @"([\[\]]|\\\\|\\\[|\\\]|\\s|\\S|\\w|\\W|\\d|\\D|.)", "`$1").Split('`');
            bool bracketed = false;
            for (int i = 1; i < tokens.Length; i++)
            {
                if (bracketed)
                {
                    switch (tokens[i])
                    {
                        case "]":
                            bracketed = false;
                            ret += tokens[i];
                            break;
                        case "\\s":
                            ret += " \n\t\r";
                            break;
                        case "\\d":
                            ret += "0-9";
                            break;
                        case "\\w":
                            ret += "A-Za-z_";
                            break;
                        default:
                            if (caseInsensitive && Regex.IsMatch(tokens[i], @"^[A-Za-z]+$"))
                            {
                                if ((i + 2) < tokens.Length && tokens[i + 1] == "-" && Regex.IsMatch(tokens[i + 2], @"^[A-Za-z]+$"))
                                {
                                    ret += tokens[i].ToLower() + "-" + tokens[i + 2].ToLower() + tokens[i].ToUpper() + "-" + tokens[i + 2].ToUpper();
                                    i += 2;
                                }
                                else
                                {
                                    ret += tokens[i].ToLower() + tokens[i].ToUpper();
                                }
                            }
                            else
                            {
                                ret += tokens[i];
                            }
                            break;
                    }
                }
                else if (tokens[i] == "[")
                {
                    bracketed = true;
                    ret += tokens[i];
                }
                else if (tokens[i] == "\\s")
                {
                    ret += "[ \n\t\r]";
                }
                else if (tokens[i] == "\\d")
                {
                    ret += "[0-9]";
                }
                else if (tokens[i] == "\\w")
                {
                    ret += "[A-Za-z_]";
                }
                else if (caseInsensitive && Regex.IsMatch(tokens[i], "[A-Za-z]"))
                {
                    ret += "[" + tokens[i].ToLower() + tokens[i].ToUpper() + "]";
                }
                else
                {
                    ret += tokens[i];
                }
            }
            return ret;
        }
        private bool IsValidOperator(string field, string opToken)
        {
            if (!_allowedBqlTokensUpperByField.TryGetValue(field, out var allowed))
            {
                return false;
            }
            return allowed.Contains(opToken);
        }

        private bool IsValidValue(string field, string value)
        {
            var type = _fieldTypeByName.TryGetValue(field, out var t) ? t : "string";

            switch (type.ToLowerInvariant())
            {
                case "boolean":
                    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                case "category":
                    // Be permissive: allow any non-empty category value even if not pre-declared
                    return !string.IsNullOrWhiteSpace(value);
                case "date":
                case "time":
                case "datetime":
                    return Regex.IsMatch(value, @"^\d{4}(-\d{2}(-\d{2}(T\d{2}:\d{2}(:\d{2})?)?)?)?$");
                case "number":
                    return double.TryParse(value, out _);
                default:
                    // strings (including document/categories) allow regex (/.../i?) or non-empty strings
                    return !string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value, @"^/.*/i?$", RegexOptions.IgnoreCase);
            }
        }

        protected static string QueryAsString(RulesetDto query, bool recurse = false)
        {
            ArgumentNullException.ThrowIfNull(query);
            var result = new StringBuilder();
            bool multipleConditions = false;

            if (query.rules == null)
            {
                return result.ToString();
            }

            foreach (RulesetDto r in query.rules)
            {
                if (r == null) continue;

                if (result.Length > 0)
                {
                    result.Append(" " + (query.condition == "and" ? "&" : "|") + " ");
                    multipleConditions = true;
                }

                if (r is RulesetDto ruleQuery && !string.IsNullOrEmpty(ruleQuery.condition))
                {
                    if (!string.IsNullOrEmpty(ruleQuery.name))
                    {
                        result.Append(ruleQuery.name);
                    }
                    else
                    {
                        result.Append(QueryAsString(ruleQuery, query.rules.Count > 1));
                    }
                }
                else if (r.field == "document")
                {
                    string valStr = r.value?.ToString() ?? string.Empty;

                    if (Regex.IsMatch(valStr, @"^/.*/i?$", RegexOptions.IgnoreCase))
                    {
                        // result.Append(valStr);
                    }
                    else if (!Regex.IsMatch(valStr, "^[a-zA-Z\\d]+$"))
                    {
                        valStr = "\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"";
                    }
                    else
                    {
                        valStr = valStr.ToLowerInvariant();
                    }
                    if (r.@operator == "!contains")
                    {
                        valStr = $"!({valStr})";
                    }
                    result.Append(valStr);
                }
                else if (!string.IsNullOrEmpty(r.name))
                {
                    result.Append(r.name);
                }
                else
                {
                    string field = r.field ?? "document";
                    string op = (r.@operator ?? "=").ToUpperInvariant();
                    result.Append(field);

                    if (op == "EXISTS")
                    {
                        result.Append(" " + (r.value is bool b && !b ? "!" : string.Empty) + "EXISTS");
                    }
                    else if (op == "IN" || op == "!IN")
                    {
                        IEnumerable<string> values = (r.value as IEnumerable<object>)?.Select(v => v?.ToString() ?? string.Empty) ?? Enumerable.Empty<string>();
                        IEnumerable<string> quoted = values.Select(v => Regex.IsMatch(v, "^[a-zA-Z\\d]+$") ? v : "\"" + Regex.Replace(v, "([\\\"])", "\\$1") + "\"");
                        result.Append(" " + op + " (" + string.Join(", ", quoted) + ")");
                    }
                    else if (op == "CONTAINS" || op == "!CONTAINS")
                    {
                        string v = (r.value as string) ?? "";
                        string quoted = Regex.IsMatch(v, "^[a-zA-Z\\d]+$") || Regex.IsMatch(v, @"^/.*/i?$") ? v : "\"" + Regex.Replace(v, "([\\\"])", "\\$1") + "\"";
                        result.Append(" " + op + " (" + string.Join(", ", quoted) + ")");
                    }
                    else if (!string.IsNullOrWhiteSpace(op))
                    {
                        result.Append(/*" " +*/ op /*+ " "*/);
                        if (r.value != null)
                        {
                            string? valStr = r.value.ToString();
                            if (valStr != null && Regex.IsMatch(valStr, @"^/.*/i?$", RegexOptions.IgnoreCase))
                            {
                                result.Append(valStr);
                            }
                            else if (valStr != null && !Regex.IsMatch(valStr, "^[A-Za-z0-9]+$"))
                            {
                                result.Append("\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
                            }
                            else
                            {
                                result.Append(valStr?.ToLowerInvariant() ?? string.Empty);
                            }
                        }
                    }
                }
            }

            if (query.not)
            {
                if (query.rules.Count == 1 && query.rules[0] is RulesetDto { name: not null })
                {
                    result.Insert(0, "!");
                }
                else
                {
                    result.Insert(0, "!(").Append(')');
                }
            }
            else if (recurse && multipleConditions)
            {
                result.Insert(0, "(").Append(')');
            }

            return result.ToString();
        }
            private bool TryGetCiField(string? field, out string? ciField)
        {
            ciField = string.Empty;
            if (string.IsNullOrWhiteSpace(field)) return false;
            return _ciFieldByName.TryGetValue(field, out ciField);
        }

        public static JObject LoadSpec(string entity)
        {
            var baseDir = AppContext.BaseDirectory;
            var specPath = Path.Combine(baseDir, "Resources", "query-builder", $"{entity}-qb-spec.json");
            if (!File.Exists(specPath))
            {
                throw new FileNotFoundException($"BQL spec file not found at {specPath}");
            }
            var json = File.ReadAllText(specPath);
            return JObject.Parse(json);
        }

        protected virtual bool ValidateBqlQuery(string bqlQuery, IEnumerable<string>? namedQueryNames)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                return true;
            }

            return BuildTokenizer(namedQueryNames).IsMatch(bqlQuery);
        }

        private void BuildSpecCaches()
        {
            var fields = (JObject?)_qbSpec["fields"] ?? new JObject();
            foreach (var kv in fields)
            {
                var fieldName = kv.Key;
                _validFields.Add(fieldName);
                var fieldObj = (JObject?)kv.Value ?? new JObject();
                var type = fieldObj.Value<string>("type") ?? "string";
                _fieldTypeByName[fieldName] = type;

                // Discover case-insensitive keyword capability from spec
                var ciExplicit = fieldObj.Value<string>("ciField");
                var hasCi = fieldObj.Value<bool?>("hasCiKeyword") == true;
                if (!string.IsNullOrWhiteSpace(ciExplicit))
                {
                    _ciFieldByName[fieldName] = ciExplicit!;
                }
                else if (hasCi)
                {
                    _ciFieldByName[fieldName] = fieldName + ".ci";
                }

                // field-specific operators (lowercase as in spec)
                var opsLower = fieldObj["operators"]?.Select(v => v?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList() ?? new List<string>();
                if (opsLower.Count > 0)
                {
                    if (opsLower.Contains("contains") && !opsLower.Contains("like"))
                    {
                        opsLower.Add("like");
                    }
                    if (opsLower.Contains("!contains") && !opsLower.Contains("!like"))
                    {
                        opsLower.Add("!like");
                    }
                    _fieldOperatorsLowerByName[fieldName] = opsLower;
                }

                // category options
                if (type.Equals("category", StringComparison.OrdinalIgnoreCase))
                {
                    var values = fieldObj["options"]?.Select(o => (o?["value"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .ToList() ?? new List<string>();
                    if (values.Count > 0)
                    {
                        _categoryAllowedValuesByField[fieldName] = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }

            // operator map by type (lowercase as in spec)
            var operatorMap = (JObject?)_qbSpec["operatorMap"] ?? new JObject();
            foreach (var kv in operatorMap)
            {
                var type = kv.Key;
                var ops = kv.Value?.Select(v => v?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList() ?? new List<string>();
                if (ops.Contains("contains") && !ops.Contains("like"))
                {
                    ops.Add("like");
                }
                if (ops.Contains("!contains") && !ops.Contains("!like"))
                {
                    ops.Add("!like");
                }
                _operatorMapLowerByType[type] = ops;
            }

            // allowed tokens per field (uppercase BQL tokens)
            foreach (var field in _validFields)
            {
                var opsLower = _fieldOperatorsLowerByName.ContainsKey(field)
                    ? _fieldOperatorsLowerByName[field]
                    : (_operatorMapLowerByType.TryGetValue(_fieldTypeByName[field], out var mapOps) ? mapOps : new List<string>());

                // Sensible defaults when spec omits operators
                if (opsLower == null || opsLower.Count == 0)
                {
                    var typeLower = _fieldTypeByName[field].ToLowerInvariant();
                    switch (typeLower)
                    {
                        case "boolean":
                            opsLower = new List<string> { "=", "!=", "exists" };
                            break;
                        case "number":
                        case "date":
                        case "time":
                        case "datetime":
                            opsLower = new List<string> { "=", "!=", ">", ">=", "<", "<=" };
                            break;
                        case "category":
                            opsLower = new List<string> { "=", "!=", "in", "!in", "exists" };
                            break;
                        default:
                            opsLower = new List<string> { "=", "!=", "contains", "!contains", "like", "!like", "in", "!in", "exists" };
                            break;
                    }
                }

                var tokens = new HashSet<string>(StringComparer.Ordinal);
                opsLower ??= new List<string>();
                foreach (var op in opsLower)
                {
                    foreach (var tok in LowerOperatorToBqlTokens(op))
                    {
                        tokens.Add(tok);
                        _allOperatorTokensUpper.Add(tok);
                    }
                }
                _allowedBqlTokensUpperByField[field] = tokens;
            }

            // default full-text field fallback: if 'document' exists
            if (_validFields.Contains("document"))
            {
                _defaultFullTextField = "document";
            }
        }

        private Regex BuildTokenizer(IEnumerable<string>? namedQueryNames = null)
        {
            string fieldsAlt = _validFields.Count > 0 ? string.Join("|", _validFields.OrderByDescending(s => s, StringComparer.Ordinal).Select(Regex.Escape)) : string.Empty;
            string namesAlt = string.Empty;
            if (namedQueryNames != null)
            {
                var names = namedQueryNames.Where(n => !string.IsNullOrEmpty(n)).Select(n => n!.ToUpperInvariant()).Distinct().ToList();
                if (names.Count > 0)
                {
                    // Sort by descending length so longer names are matched before shorter ones
                    names.Sort((a, b) => b.Length.CompareTo(a.Length));
                    namesAlt = "|()" + string.Join("|", names.Select(Regex.Escape)) + ")";
                }
            }

            string regexString = @"\s*(" +
                @"(""(\\""|\\\\|[^""])+\""|\/(\\\/|[^\/])+\/i?" +
                @"|(?:(?<=IN\s)|(?<=CONTAINS\s)|(?<=!CONTAINS\s))(\(""(\\""|[^""])+""|[^""\s]+\s*)(,\s*(""(\\""|[^""])+""|[^""\s]+\s*))*\s*\)" +
                @"|[()]" +
                (string.IsNullOrEmpty(fieldsAlt) ? string.Empty : @"|" + fieldsAlt) +
                @")" +
                namesAlt +
                @"|(=|!=|CONTAINS|!CONTAINS|LIKE|!LIKE|EXISTS|!EXISTS|IN|!IN|>=|<=|>|<)" +
                @"|(&|\||!)" +
                @"|[^""/=!<>() ]+)\s*";

            return new Regex(regexString);
        }

        private static IEnumerable<string> LowerOperatorToBqlTokens(string lowerOp)
        {
            switch (lowerOp)
            {
                case "=":
                case "!=":
                case ">":
                case ">=":
                case "<":
                case "<=":
                    return new[] { lowerOp };
                case "in":
                    return new[] { "IN" };
                case "!in":
                    return new[] { "!IN" };
                case "contains":
                    return new[] { "CONTAINS" };
                case "!contains":
                    return new[] { "!CONTAINS" };
                case "like":
                    return new[] { "LIKE" };
                case "!like":
                    return new[] { "!LIKE" };
                case "exists":
                    // allow both EXISTS and !EXISTS tokens in parsing; negation will be represented via value=false
                    return new[] { "EXISTS", "!EXISTS" };
                default:
                    return Array.Empty<string>();
            }
        }
        private List<string> ExtractInValues(string field, string combinedValuesToken)
        {
            var values = new List<string>();
            if (string.IsNullOrWhiteSpace(combinedValuesToken) ||
                !combinedValuesToken.StartsWith("(", StringComparison.Ordinal) ||
                !combinedValuesToken.EndsWith(")", StringComparison.Ordinal))
            {
                return values;
            }

            var inner = combinedValuesToken.Substring(1, combinedValuesToken.Length - 2);
            var parts = inner.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part?.Trim() ?? string.Empty;
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (trimmed.Length >= 2 && trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
                {
                    trimmed = trimmed.Substring(1, trimmed.Length - 2);
                }

                var normalized = trimmed.ToLowerInvariant();
                if (IsValidValue(field, normalized))
                {
                    values.Add(normalized);
                }
            }

            return values;
        }
    }
} 
