using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Nest;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;
using System.Text.RegularExpressions;

namespace JhipsterSampleApplication.Domain.Services
{
	public class SupremeService : ISupremeService
	{
		private readonly IElasticClient _elasticClient;
		private const string IndexName = "supreme";
		private readonly ISupremeBqlService _bqlService;
		private readonly IViewService _viewService;

		public SupremeService(IElasticClient elasticClient, ISupremeBqlService bqlService, IViewService viewService)
		{
			_elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
			_bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
			_viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
		}

		public async Task<ISearchResponse<Supreme>> SearchAsync(ISearchRequest request, string? pitId = null)
		{
			if (pitId == null)
			{
				var pitResponse = await _elasticClient.OpenPointInTimeAsync(new OpenPointInTimeRequest(IndexName)
				{
					KeepAlive = "2m"
				});
				if (!pitResponse.IsValid)
				{
					throw new Exception($"Failed to open point in time: {pitResponse.DebugInformation}");
				}
				pitId = pitResponse.Id;
			}
			if (!string.IsNullOrEmpty(pitId))
			{
				request.PointInTime = new PointInTime(pitId);
			}
			var response = await _elasticClient.SearchAsync<Supreme>(request);
			if (!response.IsValid)
			{
				throw new Exception(response.DebugInformation);
			}
			if (response.Hits.Count > 0)
			{
				foreach (var hit in response.Hits)
				{
					if (hit.Source != null)
					{
						hit.Source.Id = hit.Id;
					}
				}
			}
			return response;
		}

		public async Task<IndexResponse> IndexAsync(Supreme document)
		{
			return await _elasticClient.IndexDocumentAsync(document);
		}

		public async Task<UpdateResponse<Supreme>> UpdateAsync(string id, Supreme document)
		{
			return await _elasticClient.UpdateAsync<Supreme>(id, u => u
				.Doc(document)
				.DocAsUpsert()
			);
		}

		public async Task<DeleteResponse> DeleteAsync(string id)
		{
			return await _elasticClient.DeleteAsync<Supreme>(id);
		}

		public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
		{
			var result = await _elasticClient.SearchAsync<Aggregation>(q => q
				.Size(0).Index(IndexName).Aggregations(agg => agg.Terms(
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
						if (item is KeyedBucket<Object> kb)
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

		public async Task<ISearchResponse<Supreme>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null)
		{
			var queryObject = await ConvertRulesetToElasticSearch(ruleset);
			string query = queryObject.ToString();
			var searchRequest = new SearchRequest<Supreme>
			{
				Size = size,
				From = from,
				Query = new QueryContainerDescriptor<Supreme>().Raw(query)
			};
			if (sort != null && sort.Any())
			{
				searchRequest.Sort = sort;
			}
			else
			{
				searchRequest.Sort = new List<ISort>
				{
					new FieldSort { Field = "_id", Order = SortOrder.Ascending }
				};
			}
			return await SearchAsync(searchRequest);
		}

		public async Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr)
		{
			if (rr.rules == null || rr.rules.Count == 0)
			{
				JObject ret = new JObject{
					{
						"match_none", new JObject{}
					}
				};
                                if (rr.@operator?.Contains("contains") == true || rr.@operator?.Contains("like") == true)
				{
					string stringValue = rr.value?.ToString() ?? string.Empty;
					string quote = Regex.IsMatch(stringValue, "\\W") ? "\"" : string.Empty;
					return new JObject{
						{
							"query_string", new JObject{
								{ "query", (rr.field != "document" ? (ToEsField(rr.field) + ":") : string.Empty) + quote + stringValue.ToLower().Replace("\"", "\\\"") + quote }
							}
						}
					};
				}
				else if (rr.@operator?.Contains("=") == true)
				{
					var valueStr = rr.value as string ?? string.Empty;
					string field = ToEsField(rr.field);
					return new JObject{
						{
							"query_string", new JObject{
								{ "query", (field != "document" ? (field + ":\"") : "\"") + valueStr.ToLower().Replace("\"", "\\\"") + "\"" }
							}
						}
					};
				}
				else if (rr.@operator?.Contains("in") == true)
				{
					var valueArray = rr.value as IEnumerable<object>;
					var values = valueArray?.Select(v => v?.ToString() ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
					if (values.Count == 0)
					{
						return new JObject{ { "match_none", new JObject{} } };
					}
					string field = ToEsField(rr.field);
					var shoulds = new JArray(values.Select(v => new JObject{
						{ "query_string", new JObject{ { "query", field + ":\"" + v.ToLower().Replace("\"","\\\"") + "\"" } } }
					}).ToArray());
					return new JObject{ { "bool", new JObject{ { "should", shoulds } } } };
				}
				else if (rr.@operator?.Contains("exists") == true)
				{
					return new JObject{
						{
							"exists", new JObject{ { "field", ToEsField(rr.field) } }
						}
					};
				}
				if (rr.@operator?.Contains("!") == true)
				{
					var inner = await ConvertRulesetToElasticSearch(new Ruleset
					{
						field = rr.field,
						@operator = rr.@operator!.Replace("!", string.Empty),
						value = rr.value
					});
					return new JObject { { "bool", new JObject { { "must_not", inner } } } };
				}
				return ret;
			}
			else
			{
				List<object> rls = new List<object>();
				for (int i = 0; i < rr.rules.Count; i++)
				{
					rls.Add(await ConvertRulesetToElasticSearch(rr.rules[i]));
				}
				if (rr.condition == "and")
				{
					return new JObject{ { "bool", new JObject{ { rr.not == true ? "must_not" : "must", JArray.FromObject(rls) } } } };
				}
				JObject ret = new JObject{ { "bool", new JObject{ { "should", JArray.FromObject(rls) } } } };
				if (rr.not == true)
				{
					ret = new JObject{ { "bool", new JObject{ { "must_not", JObject.FromObject(ret) } } } };
				}
				return ret;
			}
		}

		private static string ToEsField(string? field)
		{
			if (string.IsNullOrEmpty(field) || field == "document") return "document";
			// Map BQL field names (lowercase) to ES field names (some are camel or lowercase)
			return field switch
			{
				"name" => "name",
				"term" => "term",
				"docket_number" => "docket_number",
				"petitioner" => "Petitioner",
				"respondent" => "Respondent",
				"appellant" => "Appellant",
				"appellee" => "Appellee",
				"heard_by" => "heard_by",
				"lower_court" => "lower_court",
				"manner_of_jurisdiction" => "manner_of_jurisdiction",
				"majority" => "majority",
				"minority" => "minority",
				"advocates" => "advocates",
				"question" => "question",
				"facts_of_the_case" => "facts_of_the_case",
				"conclusion" => "conclusion",
				"description" => "description",
				"opinion" => "opinion",
				"justia_url" => "justia_url",
				"recused" => "recused",
				_ => field
			};
		}

		public async Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto viewDto, int size = 20, int from = 0, IList<ISort>? sort = null)
		{
			string query = queryObject.ToString();
			var request = new SearchRequest<Supreme>
			{
				Size = 0,
				From = from,
				Query = new QueryContainerDescriptor<Supreme>().Raw(query),
				Aggregations = new AggregationDictionary{
					{
						"distinct",
						new TermsAggregation("distinct")
						{
							Size = 10000,
							Field = string.IsNullOrEmpty(viewDto.Script) ? viewDto.Aggregation : null,
							Script = !string.IsNullOrEmpty(viewDto.Script) ? new InlineScript(viewDto.Script) : null
						}
					}
				}
			};
			var uncategorizedRequest = new SearchRequest<Supreme>
			{
				Size = 0,
				From = from,
				Query = new QueryContainerDescriptor<Supreme>().Raw(query),
				Aggregations = new AggregationDictionary{
					{
						"uncategorized", new FilterAggregation("uncategorized")
						{
							Filter = new BoolQuery
							{
								Should = new List<QueryContainer>
								{
									new BoolQuery
									{
										MustNot = new List<QueryContainer>
										{
											new ExistsQuery { Field = viewDto.Aggregation }
										}
									},
									new TermQuery { Field = viewDto.Aggregation, Value = string.Empty }
								},
								MinimumShouldMatch = 1
							}
						}
					}
				}
			};
			return await SearchUsingViewAsync(request, uncategorizedRequest);
		}

		public async Task<List<ViewResultDto>> SearchUsingViewAsync(ISearchRequest request, ISearchRequest uncategorizedRequest)
		{
			List<ViewResultDto> content = new();
			var result = await _elasticClient.SearchAsync<Aggregation>(request);
			((BucketAggregate)result.Aggregations.ToList()[0].Value).Items.ToList().ForEach(it =>
			{
				KeyedBucket<object> kb = (KeyedBucket<object>)it;
				string categoryName = kb.KeyAsString != null ? kb.KeyAsString : (string)kb.Key;
				bool notCategorized = false;
				if (Regex.IsMatch(categoryName, @"\d{4,4}-\d{2,2}-\d{2,2}T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z"))
				{
					categoryName = Regex.Replace(categoryName, @"(\d{4,4})-(\d{2,2})-(\d{2,2})T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z", "$1-$2-$3");
				}
				if (string.IsNullOrEmpty(categoryName))
				{
					categoryName = "(Uncategorized)";
					notCategorized = true;
				}
				content.Add(new ViewResultDto
				{
					CategoryName = categoryName,
					Count = kb.DocCount,
					NotCategorized = notCategorized
				});

			});
			content = content.OrderBy(cat => cat.CategoryName).ToList();
			var uncategorizedResponse = await _elasticClient.SearchAsync<Supreme>(uncategorizedRequest);
			var uncatetgorizedCount = uncategorizedResponse.Aggregations.Filter("uncategorized").DocCount;
			if (uncatetgorizedCount > 0)
			{
				var existingUncategorized = content.FirstOrDefault(c => c.NotCategorized == true);
				if (existingUncategorized != null)
				{
					existingUncategorized.Count = (existingUncategorized.Count ?? 0) + uncatetgorizedCount;
				}
				else
				{
					content.Add(new ViewResultDto
					{
						CategoryName = "(Uncategorized)",
						Selected = false,
						NotCategorized = true,
						Count = uncatetgorizedCount
					});
				}
			}
			return content;
		}
	}
}