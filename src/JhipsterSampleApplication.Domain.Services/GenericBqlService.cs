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
    public abstract class GenericBqlService<TDomain> : IGenericBqlService<TDomain> where TDomain : class
    {
        protected readonly ILogger _logger;
        protected readonly INamedQueryService _namedQueryService;
        private readonly JObject _qbSpec;

        // Computed spec data
        private readonly HashSet<string> _validFields = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _fieldTypeByName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _fieldOperatorsLowerByName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _operatorMapLowerByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _allowedBqlTokensUpperByField = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _categoryAllowedValuesByField = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> _allOperatorTokensUpper = new HashSet<string>(StringComparer.Ordinal);
        		private string? _defaultFullTextField;
		private readonly Regex _tokenizer;

        protected GenericBqlService(ILogger logger, INamedQueryService namedQueryService, JObject qbSpec)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _namedQueryService = namedQueryService ?? throw new ArgumentNullException(nameof(namedQueryService));
            _qbSpec = qbSpec ?? throw new ArgumentNullException(nameof(qbSpec));

            BuildSpecCaches();
            _tokenizer = BuildTokenizer();
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

        public virtual Task<object> Ruleset2ElasticSearch(RulesetDto ruleset)
        {
            throw new NotImplementedException();
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

                // field-specific operators (lowercase as in spec)
                var opsLower = fieldObj["operators"]?.Select(v => v?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList() ?? new List<string>();
                if (opsLower.Count > 0)
                {
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
                _operatorMapLowerByType[type] = ops;
            }

            // allowed tokens per field (uppercase BQL tokens)
            foreach (var field in _validFields)
            {
                var opsLower = _fieldOperatorsLowerByName.ContainsKey(field)
                    ? _fieldOperatorsLowerByName[field]
                    : (_operatorMapLowerByType.TryGetValue(_fieldTypeByName[field], out var mapOps) ? mapOps : new List<string>());

                var tokens = new HashSet<string>(StringComparer.Ordinal);
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
                @"|(=|!=|CONTAINS|!CONTAINS|EXISTS|!EXISTS|IN|!IN|>=|<=|>|<)" +
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
                case "not in":
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
                var namedQuery = await _namedQueryService.FindByNameAndOwner(rulesetName, null);
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

                    return (true, index + 1, new RulesetDto
                    {
                        field = _defaultFullTextField,
                        @operator = "contains",
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
                        @operator = opToken == "IN" ? "in" : "not in",
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
                    if (_categoryAllowedValuesByField.TryGetValue(field, out var allowed))
                    {
                        return allowed.Contains(value);
                    }
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

        private static string QueryAsString(RulesetDto query, bool recurse = false)
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
                else if (r.field == "document" && r.value != null)
                {
                    var valStr = r.value.ToString();
                    if (valStr != null && Regex.IsMatch(valStr, @"^/.*/i?$", RegexOptions.IgnoreCase))
                    {
                        result.Append(valStr);
                    }
                    else if (valStr != null && !Regex.IsMatch(valStr, "^[a-zA-Z\\d]+$"))
                    {
                        result.Append("\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
                    }
                    else
                    {
                        result.Append(valStr?.ToLowerInvariant() ?? "");
                    }
                }
                else if (r.field != null)
                {
                    result.Append(r.field);

                    if (r.@operator == "exists")
                    {
                        result.Append(" " + (r.value is bool b && !b ? "!" : "") + "EXISTS ");
                    }
                    else if (r.@operator == "in" || r.@operator == "not in")
                    {
                        var values = r.value as IEnumerable<string> ?? new List<string>();
                        var quoted = values.Select(v => Regex.IsMatch(v, "^[a-zA-Z\\d]+$") ? v : "\"" + Regex.Replace(v ?? string.Empty, "([\\\"])", "\\$1") + "\"");
                        result.Append(" " + (r.@operator == "in" ? string.Empty : "!") + "IN (" + string.Join(", ", quoted) + ") ");
                    }
                    else if (!string.IsNullOrWhiteSpace(r.@operator))
                    {
                        result.Append(" " + r.@operator.ToUpperInvariant() + " ");
                        if (r.value != null)
                        {
                            var valStr = r.value.ToString();
                            if (valStr != null && Regex.IsMatch(valStr, @"^/.*/i?$", RegexOptions.IgnoreCase))
                            {
                                result.Append(valStr);
                            }
                            else if (valStr != null && Regex.IsMatch(valStr, "[\\s\\\"]"))
                            {
                                result.Append("\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
                            }
                            else
                            {
                                result.Append(valStr?.ToLowerInvariant() ?? "");
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