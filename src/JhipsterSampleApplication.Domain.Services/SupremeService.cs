using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Nest;
using Elasticsearch.Net;
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
                                // Retry with direct streaming disabled to expose detailed error information
                                StringResponse retryResponse;
                                if (request.PointInTime != null)
                                {
                                        // When using PIT, do not specify an index in the path
                                        retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
                                }
                                else
                                {
                                        retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(IndexName, PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
                                }
                                // Surface elastic error body directly to caller for logging
                                throw new Exception(retryResponse.Body);
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
                    var dto = ToDto(rr);
                    var result = await _bqlService.Ruleset2ElasticSearch(dto);
                    return result as JObject ?? new JObject();
            }

            private static RulesetDto ToDto(Ruleset rule)
            {
                    return new RulesetDto
                    {
                            field = rule.field,
                            @operator = rule.@operator,
                            value = rule.value,
                            condition = rule.condition,
                            @not = rule.@not,
                            rules = rule.rules?.Select(ToDto).ToList()
                    };
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
                        var request = new SearchRequest<Supreme>(IndexName)
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
                        var uncategorizedRequest = new SearchRequest<Supreme>(IndexName)
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
            if (result.Aggregations.Count > 0)
            {
                ((BucketAggregate)result.Aggregations.ToList()[0].Value).Items.ToList().ForEach(it =>
                {
                    KeyedBucket<object> kb = (KeyedBucket<object>)it;
                    string categoryName = kb.KeyAsString ?? (kb.Key?.ToString() ?? string.Empty);
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
            }
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

		private string ToElasticRegEx(string pattern, bool caseInsensitive)
		{
			string ret = "";
			string[] regexTokens = Regex.Replace(pattern, @"([\[\]]|\\\\|\\\[|\\\]|\\s|\\S|\\w|\\W|\\d|\\D|.)", "`$1").Split('`');
			bool inBracketClass = false;
			for (int i = 1; i < regexTokens.Length; i++)
			{
				if (inBracketClass)
				{
					switch (regexTokens[i])
					{
						case "]":
							inBracketClass = false;
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
					inBracketClass = true;
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