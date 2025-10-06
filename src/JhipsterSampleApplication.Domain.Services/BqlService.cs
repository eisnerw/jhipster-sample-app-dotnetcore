using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
		private readonly Regex _tokenizer;

        public BqlService(ILogger<BqlService<TDomain>> logger, INamedQueryService namedQueryService, JObject qbSpec, string domain)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _namedQueryService = namedQueryService ?? throw new ArgumentNullException(nameof(namedQueryService));
            _qbSpec = qbSpec ?? throw new ArgumentNullException(nameof(qbSpec));
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));

            BuildSpecCaches();
            _tokenizer = BuildTokenizer();
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

        public virtual async Task<RulesetDto> Bql2Ruleset(string bqlQuery)
        {
            if (!ValidateBqlQuery(bqlQuery))
            {
                throw new ArgumentException($"Invalid BQL query: '{bqlQuery}'", nameof(bqlQuery));
            }

            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                return new RulesetDto
                {
                    condition = "or",
                    not = false,
                    rules = new List<RulesetDto>()
                };
            }

            var matches = _tokenizer.Matches(bqlQuery);
            var tokens = matches
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();

            if (tokens.Length == 0)
            {
                throw new ArgumentException("Invalid BQL query format", nameof(bqlQuery));
            }

            var result = await ParseRuleset(tokens, 0, false);
            if (!result.matches || result.index < tokens.Length)
            {
                throw new ArgumentException($"Invalid BQL query '{bqlQuery}' at position {result.index}", nameof(bqlQuery));
            }

            return result.ruleset;
        }

        public virtual Task<string> Ruleset2Bql(RulesetDto ruleset)
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

            return Task.FromResult(QueryAsString(ruleset));
        }

        public virtual async Task<object> Ruleset2ElasticSearch(RulesetDto ruleset)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            // The generic implementation assumes the ruleset has already been validated
            // by the calling service.  All rules share the same conversion logic, so we
            // recursively build an Elasticsearch query represented as a JObject.

            if (ruleset.rules == null || ruleset.rules.Count == 0)
            {
                // Handle negated operators (e.g. !LIKE, !IN, !EXISTS) by generating the
                // positive query and wrapping it in a must_not bool clause.
                if (ruleset.@operator?.Contains("!") == true ||
                    (ruleset.@operator == "exists" && ruleset.value is bool b && !b))
                {
                    var inner = await Ruleset2ElasticSearch(new RulesetDto
                    {
                        field = ruleset.field,
                        @operator = ruleset.@operator?.Replace("!", string.Empty),
                        value = ruleset.@operator == "exists" ? true : ruleset.value
                    });

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

                JObject ret = new JObject
                {
                    { "term", new JObject{ { "BOGUSFIELD", "CANTMATCH" } } }
                };

                if (ruleset.@operator?.Contains("contains") == true ||
                    ruleset.@operator?.Contains("like") == true)
                {
                    string stringValue = ruleset.value?.ToString() ?? string.Empty;

                    // Support regex literals: /exp/ or /exp/i
                    if (stringValue.StartsWith("/") &&
                       (stringValue.EndsWith("/") || stringValue.EndsWith("/i")))
                    {
                        bool caseInsensitive = stringValue.EndsWith("/i");
                        string re = stringValue.Substring(1, stringValue.Length - (caseInsensitive ? 3 : 2));
                        string regex = ToElasticRegEx(re.Replace(@"\\", @"\"), caseInsensitive);

                        if (!regex.StartsWith("^"))
                        {
                            regex = ".*" + regex;
                        }
                        else
                        {
                            regex = regex.Substring(1);
                        }

                        if (!regex.EndsWith("$"))
                        {
                            regex += ".*";
                        }
                        else
                        {
                            regex = regex.Substring(0, regex.Length - 1);
                        }

                        return new JObject
                        {
                            {
                                "regexp",
                                new JObject
                                {
                                    {
                                        ruleset.field + ".keyword",
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

                    var rangeOperator = ruleset.@operator switch
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
                else if (ruleset.@operator?.Contains("=") == true)
                {
                    var valueStr = ruleset.value?.ToString() ?? string.Empty;

                    // Boolean fields: use exact term query with boolean value
                    if (!string.IsNullOrWhiteSpace(ruleset.field) &&
                        _fieldTypeByName.TryGetValue(ruleset.field!, out var ftype) &&
                        string.Equals(ftype, "boolean", StringComparison.OrdinalIgnoreCase))
                    {
                        var boolVal = string.Equals(valueStr, "true", StringComparison.OrdinalIgnoreCase);
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

                    var valueLower = valueStr.ToLowerInvariant();

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
                    var values = valueArray?.Select(v => v?.ToString() ?? string.Empty)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList() ?? new List<string>();

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
                        var matchQuery = string.Join(" ", lowerValues);
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
                foreach (var rule in ruleset.rules)
                {
                    rls.Add(await Ruleset2ElasticSearch(rule));
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

                JObject ret = new JObject
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

        private static JObject BuildDateQuery(string field, string op, string value)
        {
            var (start, endExclusive) = ParseDateValue(value);
            var startStr = start.ToString("yyyy-MM-dd'T'HH:mm:ss");

            JObject rangeBody = new JObject();

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

        protected virtual bool ValidateBqlQuery(string bqlQuery)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                return true;
            }

            return _tokenizer.IsMatch(bqlQuery);
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
                if ((opsLower == null || opsLower.Count == 0) &&
                    string.Equals(_fieldTypeByName[field], "boolean", StringComparison.OrdinalIgnoreCase))
                {
                    opsLower = new List<string> { "=", "!=" };
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

        private Regex BuildTokenizer()
        {
            string fieldsAlt = _validFields.Count > 0 ? string.Join("|", _validFields.Select(Regex.Escape)) : string.Empty;

            var regexString = @"\s*(" +
                @"(?<=IN\s)(\(""(\\""|[^""])+""|\/(\\\/|[^/]+\/)|[^""\s]+\s*)(,\s*(""(\\""|[^""])+""|[^""\s]+\s*))*\s*\)" +
                @"|[()]" +
                @"|(""(\\""|\\\\|[^""])+\""|\/(\\\/|[^\/])+\/i?" +
                (string.IsNullOrEmpty(fieldsAlt) ? string.Empty : @"|" + fieldsAlt) +
                @")" +
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

        private async Task<(bool matches, int index, RulesetDto ruleset)> ParseRuleset(string[] tokens, int index, bool not)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            var result = await ParseAndOrRuleset(tokens, index, not);
            if (!result.matches)
            {
                result = await ParseNotRuleset(tokens, index);
                if (!result.matches)
                {
                    result = await ParseParened(tokens, index, not);
                }
            }

            return result;
        }

        private async Task<(bool matches, int index, RulesetDto ruleset)> ParseAndOrRuleset(string[] tokens, int index, bool not)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            var rules = new List<RulesetDto>();
            var result = await ParseParened(tokens, index, not);
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
                if (not && result.index < tokens.Length && tokens[result.index] == ")")
                {
                    return (true, result.index, new RulesetDto
                    {
                        condition = "or",
                        rules = new List<RulesetDto> { result.ruleset },
                        not = true
                    });
                }
                return result;
            }

            var condition = tokens[result.index];
            rules.Add(result.ruleset);
            index = result.index + 1;

            while (index < tokens.Length)
            {
                result = await ParseParened(tokens, index, not);
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

        private async Task<(bool matches, int index, RulesetDto ruleset)> ParseNotRuleset(string[] tokens, int index)
        {
            if (index >= tokens.Length || tokens[index] != "!")
            {
                return (false, index, new RulesetDto());
            }

            index++;
            var result = await ParseParened(tokens, index, true);
            if (!result.matches)
            {
                return (false, index, new RulesetDto());
            }

            result.ruleset.not = true;
            return result;
        }

        private async Task<(bool matches, int index, RulesetDto ruleset)> ParseParened(string[] tokens, int index, bool not)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            // NOT at the start handled here as well
            if (tokens[index] == "!")
            {
                index++;
                not = true;
            }

            // Named ruleset reference (UPPER_SNAKE_CASE)
            if (Regex.IsMatch(tokens[index], @"^(?=.*[A-Z])[A-Z0-9_]+$"))
            {
                var rulesetName = tokens[index];
                var namedQuery = await _namedQueryService.FindByNameAndOwner(rulesetName, null, _domain);
                if (namedQuery != null)
                {
                    var ruleset = await Bql2Ruleset(namedQuery.Text);
                    if (not)
                    {
                        return (true, index + 1, new RulesetDto
                        {
                            condition = "or",
                            rules = new List<RulesetDto> { ruleset },
                            not = true
                        });
                    }
                    return (true, index + 1, ruleset);
                }
                return (false, index, new RulesetDto());
            }

            // Check for parenthesized expression
            if (tokens[index] != "(")
            {
                return (false, index, new RulesetDto());
            }

            index++;
            var result = await ParseRuleset(tokens, index, not);
            if (!result.matches)
            {
                result = ParseRule(tokens, index);
                if (result.matches)
                {
                    result.ruleset = new RulesetDto
                    {
                        condition = "or",
                        rules = new List<RulesetDto> { result.ruleset },
                        not = not
                    };
                }
                else
                {
                    return result;
                }
            }

            if (result.index >= tokens.Length || tokens[result.index] != ")")
            {
                return (false, result.index, new RulesetDto());
            }

            result.index++;
            return result;
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
                if (_defaultFullTextField != null &&
                    (Regex.IsMatch(tokens[index], @"^[A-Za-z0-9?*]+$") || tokens[index].StartsWith("\"") || tokens[index].StartsWith("/")))
                {
                    var docValue = tokens[index];
                    if (docValue.StartsWith("\""))
                    {
                        if (docValue.EndsWith("\\\""))
                        {
                            return (false, index, new RulesetDto());
                        }
                    }
                    else if (docValue.StartsWith("/"))
                    {
                        try
                        {
                            var pattern = docValue.Substring(1);
                            if (pattern.EndsWith("/i"))
                            {
                                pattern = pattern.Substring(0, pattern.Length - 2);
                            }
                            else if (pattern.EndsWith("/"))
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

                    var op = Regex.IsMatch(docValue ?? string.Empty, @"^/.*/i?$", RegexOptions.IgnoreCase) ? "like" : "contains";
                    return (true, index + 1, new RulesetDto
                    {
                        field = _defaultFullTextField,
                        @operator = op,
                        value = docValue ?? string.Empty
                    });
                }
                return (false, index, new RulesetDto());
            }

            var field = tokens[index];
            index++;

            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            var opToken = tokens[index];
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
                    value = !opToken.StartsWith("!")
                });
            }

            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            // IN / !IN list
            if (opToken == "IN" || opToken == "!IN")
            {
                if (tokens[index].StartsWith("(") && tokens[index].EndsWith(")"))
                {
                    var raw = tokens[index].Substring(1, tokens[index].Length - 2);
                    var values = raw
                        .Split(',')
                        .Select(v => v.Trim())
                        .Select(v =>
                        {
                            var t = v;
                            if (t.Length >= 2 && t.StartsWith('"') && t.EndsWith('"'))
                            {
                                t = t.Substring(1, t.Length - 2);
                            }
                            return t.ToLowerInvariant();
                        })
                        .Where(v => IsValidValue(field, v))
                        .ToList();

                    if (values.Count == 0)
                    {
                        return (false, index, new RulesetDto());
                    }

                    return (true, index + 1, new RulesetDto
                    {
                        field = field,
                        @operator = opToken == "IN" ? "in" : "!in",
                        value = values ?? new List<string>()
                    });
                }
                return (false, index, new RulesetDto());
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
            var rulesetOperator = OpTokenToRulesetOperator(opToken);

            return (true, index + 1, new RulesetDto
            {
                field = field,
                @operator = rulesetOperator,
                value = value ?? string.Empty
            });
        }

        private static string OpTokenToRulesetOperator(string opToken)
        {
            switch (opToken)
            {
                case "CONTAINS": return "contains";
                case "!CONTAINS": return "!contains";
                case "LIKE": return "like";
                case "!LIKE": return "!like";
                default: return opToken.ToLowerInvariant();
            }
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
                    return Regex.IsMatch(value, @"^\d{4}(-\d{2}(-\d{2}(T\d{2}:\d{2}:\d{2})?)?)?$");
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

            foreach (var r in query.rules)
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
                    var valStr = r.value?.ToString() ?? string.Empty;
                    if (Regex.IsMatch(valStr, @"^/.*/i?$", RegexOptions.IgnoreCase))
                    {
                        result.Append(valStr);
                    }
                    else if (!Regex.IsMatch(valStr, "^[a-zA-Z\\d]+$"))
                    {
                        result.Append("\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
                    }
                    else
                    {
                        result.Append(valStr.ToLowerInvariant());
                    }
                }
                else
                {
                    var field = r.field ?? "document";
                    var op = (r.@operator ?? "=").ToUpperInvariant();
                    result.Append(field);

                    if (op == "EXISTS")
                    {
                        result.Append(" " + (r.value is bool b && !b ? "!" : string.Empty) + "EXISTS");
                    }
                    else if (op == "IN" || op == "!IN")
                    {
                        var values = (r.value as IEnumerable<object>)?.Select(v => v?.ToString() ?? string.Empty) ?? Enumerable.Empty<string>();
                        var quoted = values.Select(v => Regex.IsMatch(v, "^[a-zA-Z\\d]+$") ? v : "\"" + Regex.Replace(v, "([\\\"])", "\\$1") + "\"");
                        result.Append(" " + op + " (" + string.Join(", ", quoted) + ")");
                    }
                    else if (!string.IsNullOrWhiteSpace(op))
                    {
                        result.Append(" " + op + " ");
                        if (r.value != null)
                        {
                            var valStr = r.value.ToString();
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
                if (query.rules.Count == 1 && (query.rules[0] as RulesetDto)?.name != null)
                {
                    result.Insert(0, "!");
                }
                else
                {
                    result.Insert(0, "!(").Append(")");
                }
            }
            else if (recurse && multipleConditions)
            {
                result.Insert(0, "(").Append(")");
            }

            return result.ToString();
        }
    }
} 
