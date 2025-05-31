using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;

namespace JhipsterSampleApplication.Domain.Services
{
    public class BirthdayBqlService : GenericBqlService<Birthday>, IBirthdayBqlService
    {
        private static readonly string[] ValidSigns = new[] { "aries", "taurus", "gemini", "cancer", "leo", "virgo", "libra", "scorpio", "sagittarius", "capricorn", "aquarius", "pisces" };
        private static readonly string[] ValidFields = new[] { "sign", "dob", "lname", "fname", "isAlive", "document", "categories" };
        private static readonly string[] ValidOperators = new[] { "=", "!=", "CONTAINS", "!CONTAINS", "LIKE", "EXISTS", "!EXISTS", "IN", "!IN", ">=", "<=", ">", "<" };
        private readonly INamedQueryService _namedQueryService;

        public BirthdayBqlService(ILogger<BirthdayBqlService> logger, INamedQueryService namedQueryService) : base(logger)
        {
            _namedQueryService = namedQueryService;
        }

        public override async Task<RulesetDto> Bql2Ruleset(string bqlQuery)
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

            // Tokenize the query
            var regexString = @"\s*(" + 
                @"(?<=IN\s)(\(""(\\""|[^""])+""|\/(\\/|[^/]+\/)|[^""\s]+\s*)(,\s*(""(\\""|[^""])+""|[^""\s]+\s*))*\s*\)" +
                @"|[()]" +
                @"|(""(\\""|\\\\|[^""])+\""|\/(\\\/|[^\/])+\/i?" +
                @"|sign|dob|lname|fname|isAlive|document)|(=|!=|CONTAINS|!CONTAINS|EXISTS|!EXISTS|IN|!IN|>=|<=|>|<)|(&|\||!)|[^""/=!<>() ]+)\s*";
            
            var regex = new Regex(regexString);
            var matches = regex.Matches(bqlQuery);
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

        private async Task<(bool matches, int index, RulesetDto ruleset)> ParseRuleset(string[] tokens, int index, bool not)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            // Try parseAndOrRuleset first
            var result = await ParseAndOrRuleset(tokens, index, not);
            if (!result.matches)
            {
                // Try parseNotRuleset next
                result = await ParseNotRuleset(tokens, index);
                if (!result.matches)
                {
                    // Finally try parseParened
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

            // Handle NOT at the start
            if (tokens[index] == "!")
            {
                index++;
                not = true;
            }

            // Check for named ruleset reference
            if (Regex.IsMatch(tokens[index], @"^(?=.*[A-Z])[A-Z0-9_]+$")) // contains at least one A-Z, with the rest A-Z0-9_
            {
                var rulesetName = tokens[index];
                var namedQuery = await _namedQueryService.FindByNameAndOwner(rulesetName, null);
                if (namedQuery != null)
                {
                    var ruleset = await Bql2Ruleset(namedQuery.Text);
                    if (not)
                    {
                        // The named query must be nested in parentheses to add NOT
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

            // Handle document field with direct value
            if (!ValidFields.Contains(tokens[index]))
            {
                if (Regex.IsMatch(tokens[index], @"^[A-Za-z0-9?*]+$") || 
                    tokens[index].StartsWith("\"") || 
                    tokens[index].StartsWith("/"))
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
                            var pattern = docValue.Substring(1); // Remove leading '/'
                            if (pattern.EndsWith("/i"))
                            {
                                pattern = pattern.Substring(0, pattern.Length - 2); // Remove trailing '/i'
                            }
                            else if (pattern.EndsWith("/"))
                            {
                                pattern = pattern.Substring(0, pattern.Length - 1); // Remove trailing '/'
                            }
                            new Regex(pattern);
                        }
                        catch
                        {
                            return (false, index, new RulesetDto());
                        }
                    }

                    return (true, index + 1, new RulesetDto
                    {
                        field = "document",
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

            var op = tokens[index];
            if (!IsValidOperator(field, op))
            {
                return (false, index, new RulesetDto());
            }
            index++;

            // Handle EXISTS/!EXISTS operators
            if (op == "EXISTS" || op == "!EXISTS")
            {
                return (true, index, new RulesetDto
                {
                    field = field,
                    @operator = "exists",
                    value = !op.StartsWith("!")
                });
            }

            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }            

            // Handle IN/!IN operators
            if (op == "IN" || op == "!IN")
            {
                if (tokens[index].StartsWith("(") && tokens[index].EndsWith(")"))
                {
                    var values = tokens[index].Substring(1, tokens[index].Length - 2)
                        .Split(',')
                        .Select(v => v.Trim().ToLower())
                        .Where(v => IsValidValue(field, v.StartsWith('"') && v.EndsWith('"') ? v.Substring(1, v.Length - 2) : v))
                        .Select(v => $"\"{v}\"")
                        .ToList();

                    if (values.Count == 0)
                    {
                        return (false, index, new RulesetDto());
                    }

                    return (true, index + 1, new RulesetDto
                    {
                        field = field,
                        @operator = op.ToLower(),
                        value = values ?? new List<string>()
                    });
                }
                return (false, index, new RulesetDto());
            }

            // Handle single value
            string value = tokens[index];
            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value.Substring(1, value.Length - 2);
            }

            if (!IsValidValue(field, value))
            {
                return (false, index, new RulesetDto());
            }

            return (true, index + 1, new RulesetDto
            {
                field = field,
                @operator = op.ToLower(),
                value = value ?? string.Empty
            });
        }

        private bool IsValidOperator(string field, string op)
        {
            return field switch
            {
                "isAlive" => op == "=",
                "sign" => new[] { "=", "!=", "IN", "!IN" }.Contains(op),
                "dob" => new[] { "=", "!=", ">=", "<=", ">", "<", "EXISTS", "!EXISTS" }.Contains(op),
                "lname" => new[] { "=", "!=", "IN", "!IN", "EXISTS", "!EXISTS" }.Contains(op),
                "fname" => new[] { "=", "!=", "CONTAINS", "!CONTAINS", "LIKE", "IN", "!IN", "EXISTS", "!EXISTS" }.Contains(op),
                "document" => new[] { "CONTAINS", "!CONTAINS" }.Contains(op),
                "categories" => new[] { "CONTAINS", "!CONTAINS", "EXISTS", "!EXISTS" }.Contains(op),
                _ => false
            };
        }

        private bool IsValidValue(string field, string value)
        {
            return field switch
            {
                "isAlive" => value == "true" || value == "false",
                "sign" => ValidSigns.Contains(value.ToLower()),
                "dob" => Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}$"),
                "lname" => !string.IsNullOrWhiteSpace(value),
                "fname" => !string.IsNullOrWhiteSpace(value),
                "document" => !string.IsNullOrWhiteSpace(value),
                "categories" => !string.IsNullOrWhiteSpace(value),
                _ => false
            };
        }

        public override Task<string> Ruleset2Bql(RulesetDto ruleset)
        {
            ArgumentNullException.ThrowIfNull(ruleset);

            if (!ValidateRuleset(ruleset))
            {
                throw new ArgumentException("Invalid ruleset", nameof(ruleset));
            }
            if (ruleset.rules == null || ruleset.rules.Count == 0){
                ruleset = new RulesetDto{
                    condition = "or",
                    rules = [ruleset]
                };
            }

            return Task.FromResult(QueryAsString(ruleset));
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
                    if (valStr != null && Regex.IsMatch(valStr, "^/.*/"))
                    {
                        result.Append(valStr); // regex
                    }
                    else if (valStr != null && !Regex.IsMatch(valStr, "^[a-zA-Z\\d]+$"))
                    {
                        result.Append("\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
                    }
                    else
                    {
                        result.Append(valStr?.ToLower() ?? "");
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
                        var quoted = values.Select(v => Regex.IsMatch(v, "^[a-zA-Z\\d]+$") ? v : "\"" + Regex.Replace(v ?? "", "([\\\"])", "\\$1") + "\"");
                        result.Append(" " + (r.@operator == "in" ? "" : "!") + "IN (" + string.Join(", ", quoted) + ") ");
                    }
                    else if (!string.IsNullOrWhiteSpace(r.@operator))
                    {
                        result.Append(" " + r.@operator.ToUpper() + " ");
                        if (r.value != null)
                        {
                            var valStr = r.value.ToString();
                            if (valStr != null && valStr.StartsWith("/") && (valStr.EndsWith("/") || valStr.EndsWith("/i")))
                            {
                                result.Append(valStr); // regex
                            }
                            else if (valStr != null && Regex.IsMatch(valStr, "[\\s\\\"]"))
                            {
                                result.Append("\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
                            }
                            else
                            {
                                result.Append(valStr?.ToLower() ?? "");
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

        

        protected override bool ValidateBqlQuery(string bqlQuery)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                return true;
            }

            var regex = new Regex(@"\s*(" + 
                @"(?<=IN\s)(\(""(\\""|[^""])+""|\/(\\/|[^/]+\/)|[^""\s]+\s*)(,\s*(""(\\""|[^""])+""|[^""\s]+\s*))*\s*\)" +
                @"|[()]" +
                @"|(""(\\""|\\\\|[^""])+\""|\/(\\\/|[^\/])+\/i?" +
                @"|sign|dob|lname|fname|isAlive|document)|(=|!=|CONTAINS|!CONTAINS|EXISTS|!EXISTS|IN|!IN|>=|<=|>|<)|(&|\||!)|[^""/=!<>() ]+)\s*");
            
            return regex.IsMatch(bqlQuery);
        }

        protected override bool ValidateRuleset(RulesetDto ruleset)
        {
            if (!base.ValidateRuleset(ruleset))
            {
                return false;
            }

            // Add Birthday-specific Ruleset validation here
            // For example, check that only valid Birthday attributes are referenced
            return true;
        }
    }
} 