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

namespace JhipsterSampleApplication.Domain.Services
{
    public class BirthdayBqlService : GenericBqlService<Birthday>, IBirthdayBqlService
    {
        private static readonly string[] ValidSigns = new[] { "aries", "taurus", "gemini", "cancer", "leo", "virgo", "libra", "scorpio", "sagittarius", "capricorn", "aquarius", "pisces" };
        private static readonly string[] ValidFields = new[] { "sign", "dob", "lname", "fname", "isAlive", "document", "categories" };
        private static readonly string[] ValidOperators = new[] { "=", "!=", "CONTAINS", "!CONTAINS", "LIKE", "EXISTS", "!EXISTS", "IN", "!IN", ">=", "<=", ">", "<" };
        private readonly Dictionary<string, RulesetDto> _rulesetMap = new();

        public BirthdayBqlService(ILogger<BirthdayBqlService> logger) : base(logger)
        {
        }

        public override Task<RulesetDto> Bql2Ruleset(string bqlQuery)
        {
            if (!ValidateBqlQuery(bqlQuery))
            {
                throw new ArgumentException("Invalid BQL query", nameof(bqlQuery));
            }

            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                return Task.FromResult(new RulesetDto
                {
                    condition = "or",
                    not = false,
                    rules = new List<RulesetDto>()
                });
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

            var result = ParseRuleset(tokens, 0, false);
            if (!result.matches || result.index < tokens.Length)
            {
                throw new ArgumentException($"Invalid BQL query at position {result.index}", nameof(bqlQuery));
            }

            return Task.FromResult(result.ruleset);
        }

        private (bool matches, int index, RulesetDto ruleset) ParseRuleset(string[] tokens, int index, bool not)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            // Try parseAndOrRuleset first
            var result = ParseAndOrRuleset(tokens, index, not);
            if (!result.matches)
            {
                // Try parseNotRuleset next
                result = ParseNotRuleset(tokens, index);
                if (!result.matches)
                {
                    // Finally try parseParened
                    result = ParseParened(tokens, index, not);
                }
            }

            return result;
        }

        private (bool matches, int index, RulesetDto ruleset) ParseAndOrRuleset(string[] tokens, int index, bool not)
        {
            if (index >= tokens.Length)
            {
                return (false, index, new RulesetDto());
            }

            var rules = new List<RulesetDto>();
            var result = ParseParened(tokens, index, not);
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
                result = ParseParened(tokens, index, not);
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

        private (bool matches, int index, RulesetDto ruleset) ParseNotRuleset(string[] tokens, int index)
        {
            if (index >= tokens.Length || tokens[index] != "!")
            {
                return (false, index, new RulesetDto());
            }

            index++;
            var result = ParseParened(tokens, index, true);
            if (!result.matches)
            {
                return (false, index, new RulesetDto());
            }

            result.ruleset.not = true;
            return result;
        }

        private (bool matches, int index, RulesetDto ruleset) ParseParened(string[] tokens, int index, bool not)
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
            if (tokens[index].StartsWith("@"))
            {
                var rulesetName = tokens[index].Substring(1);
                if (_rulesetMap.TryGetValue(rulesetName, out var ruleset))
                {
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
            var result = ParseRuleset(tokens, index, not);
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
            if (ruleset == null)
            {
                throw new ArgumentNullException(nameof(ruleset));
            }

            if (!ValidateRuleset(ruleset))
            {
                throw new ArgumentException("Invalid ruleset", nameof(ruleset));
            }

            return Task.FromResult(QueryAsString(ruleset));
        }

        private string QueryAsString(RulesetDto query, bool recurse = false)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var result = new List<string>();
            if (query.rules != null && query.rules.Any())
            {
                foreach (var rule in query.rules)
                {
                    if (rule != null)
                    {
                        result.Add(QueryAsString(rule, true));
                    }
                }
            }

            var multipleConditions = false;

            for (int i = 0; i < result.Count; i++)
            {
                if (i > 0)
                {
                    result[i] = $" {(query.condition == "and" ? "&" : "|")} {result[i]}";
                    multipleConditions = true;
                }
            }

            if (query.@not)
            {
                if (result.Count == 1 && result[0] != null)
                {
                    result[0] = "!" + result[0];
                }
                else
                {
                    result[0] = $"!({string.Join(" ", result)})";
                }
            }
            else if (recurse && multipleConditions)
            {
                result[0] = $"({string.Join(" ", result)})";
            }

            return string.Join(" ", result);
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