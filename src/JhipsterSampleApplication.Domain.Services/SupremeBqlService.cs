using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Domain.Services
{
	public class SupremeBqlService : GenericBqlService<Supreme>, ISupremeBqlService
	{
		private static readonly string[] ValidFields = new[] {
			"name","term","docket_number","petitioner","respondent","appellant","appellee",
			"heard_by","lower_court","manner_of_jurisdiction","document","majority","minority","advocates",
			"description","facts_of_the_case","question","conclusion","decision","opinion","justia_url","recused"
		};
		private static readonly string[] ValidOperators = new[] { "=", "!=", "CONTAINS", "!CONTAINS", "LIKE", "EXISTS", "!EXISTS", "IN", "!IN" };

		public SupremeBqlService(ILogger<SupremeBqlService> logger) : base(logger)
		{
		}

		public override async Task<RulesetDto> Bql2Ruleset(string bqlQuery)
		{
			if (string.IsNullOrWhiteSpace(bqlQuery))
			{
				return new RulesetDto
				{
					condition = "or",
					not = false,
					rules = new List<RulesetDto>()
				};
			}

			// Build token regex including dynamic field list
			var fieldsRegex = string.Join("|", ValidFields);
			var regexString = @"\s*(" +
				@"(?<=IN\s)(\(\"(\\\"|[^\"])++\"|\/(\\\/|[^\/]+\/)\|[^\"\s]+\s*)(,\s*(\"(\\\"|[^\"])++\"|[^\"\s]+\s*))*\s*\)" +
				@"|[()]" +
				@"|(\"(\\\"|\\\\|[^\"])++\"|\/(\\\/|[^\/])+\/i?" +
				@"|" + fieldsRegex + ")" +
				@"|(=|!=|CONTAINS|!CONTAINS|EXISTS|!EXISTS|IN|!IN|>=|<=|>|<)|(&|\||!)|[^\"/=!<>() ]+)\s*";

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

			if (tokens[index] == "!")
			{
				index++;
				not = true;
			}

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

			if (op == "IN" || op == "!IN")
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
							return t.ToLower();
						})
						.Where(v => !string.IsNullOrWhiteSpace(v))
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
			return ValidOperators.Contains(op);
		}

		private bool IsValidValue(string field, string value)
		{
			return !string.IsNullOrWhiteSpace(value);
		}

		public override Task<string> Ruleset2Bql(RulesetDto ruleset)
		{
			if (ruleset == null)
			{
				throw new ArgumentNullException(nameof(ruleset));
			}
			return Task.FromResult(QueryAsString(ruleset));
		}

		private static string QueryAsString(RulesetDto query)
		{
			var result = new System.Text.StringBuilder();
			if (query.rules == null) return string.Empty;
			bool multipleConditions = false;
			foreach (var r in query.rules)
			{
				if (r == null) continue;
				if (result.Length > 0)
				{
					result.Append(" " + (query.condition == "and" ? "&" : "|") + " ");
					multipleConditions = true;
				}
				if (!string.IsNullOrEmpty(r.condition))
				{
					result.Append(QueryAsString(r));
				}
				else if (r.field == "document")
				{
					var valStr = r.value?.ToString() ?? string.Empty;
					if (!Regex.IsMatch(valStr, "^[a-zA-Z\\d]+$"))
					{
						result.Append("\"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
					}
					else
					{
						result.Append(valStr.ToLower());
					}
				}
				else
				{
					string op = (r.@operator ?? "=").ToUpper();
					string field = r.field ?? "document";
					var valStr = r.value?.ToString() ?? string.Empty;
					if (op == "IN" || op == "!IN")
					{
						var values = (r.value as IEnumerable<object>)?.Select(v => v?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>();
						var escaped = values.Select(v => Regex.IsMatch(v, "^[A-Za-z0-9]+$") ? v : "\"" + Regex.Replace(v, "([\\\"])", "\\$1") + "\"");
						result.Append($"{field} {op} (" + string.Join(", ", escaped) + ")");
					}
					else if (!Regex.IsMatch(valStr, "^[A-Za-z0-9]+$"))
					{
						result.Append($"{field} {op} \"" + Regex.Replace(valStr, "([\\\"])", "\\$1") + "\"");
					}
					else
					{
						result.Append($"{field} {op} {valStr.ToLower()}");
					}
				}
			}
			return query.rules.Count > 1 ? "(" + result.ToString() + ")" : result.ToString();
		}

		public override Task<object> Ruleset2ElasticSearch(RulesetDto ruleset)
		{
			throw new NotImplementedException();
		}
	}
}