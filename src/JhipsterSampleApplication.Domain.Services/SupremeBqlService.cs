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

namespace JhipsterSampleApplication.Domain.Services
{
	public class SupremeBqlService : GenericBqlService<Supreme>, ISupremeBqlService
	{
		private static JObject LoadSpec()
		{
			var baseDir = AppContext.BaseDirectory;
			var specPath = Path.Combine(baseDir, "Resources", "query-builder", "supreme-qb-spec.json");
			if (!File.Exists(specPath))
			{
				throw new FileNotFoundException($"BQL spec file not found at {specPath}");
			}
			var json = File.ReadAllText(specPath);
			return JObject.Parse(json);
		}

		public SupremeBqlService(ILogger<SupremeBqlService> logger, INamedQueryService namedQueryService) : base(logger, namedQueryService, LoadSpec())
		{
		}


		public override Task<string> Ruleset2Bql(RulesetDto ruleset)
		{
			if (ruleset == null)
			{
				throw new ArgumentNullException(nameof(ruleset));
			}
			return Task.FromResult(QueryAsString(ruleset));
		}

		private static string QueryAsString(RulesetDto query, bool recurse = false)
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
					result.Append(QueryAsString(r, query.rules.Count > 1));
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

		public override Task<object> Ruleset2ElasticSearch(RulesetDto ruleset)
		{
			throw new NotImplementedException();
		}
	}
}