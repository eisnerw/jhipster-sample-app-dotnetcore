using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Nest;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace JhipsterSampleApplication.Domain.Services
{
    public class MovieService : IMovieService
    {
        private readonly IElasticClient _elasticClient;
        private readonly IMovieBqlService _bqlService;
        private const string IndexName = "movies";

        public MovieService(IElasticClient elasticClient, IMovieBqlService bqlService)
        {
            _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
            _bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
        }

        public Task<ISearchResponse<Movie>> SearchAsync(ISearchRequest request, string? pitId = null)
        {
            return SearchAsync(request, false, pitId);
        }

        public async Task<ISearchResponse<Movie>> SearchAsync(ISearchRequest request, bool includeDescriptive, string? pitId = null)
        {
            if (!includeDescriptive)
            {
                request.Source = new SourceFilter { Excludes = new[] { "synopsis" } };
            }

            if (string.IsNullOrEmpty(pitId))
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

            var response = await _elasticClient.SearchAsync<Movie>(request);
            // DEBUG var curl = ToCurl(_elasticClient, response, request);  // inspect `curl` in debugger
            // DEBUG System.Console.WriteLine(curl);            
            if (!response.IsValid)
            {
                StringResponse retryResponse;
                if (request.PointInTime != null)
                {
                    retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
                }
                else
                {
                    retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(IndexName, PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
                }
                throw new Exception(retryResponse.Body);
            }

            foreach (var hit in response.Hits)
            {
                if (hit.Source != null)
                {
                    hit.Source.Id = hit.Id;
                }
            }
            return response;
        }

        static string ToCurl(IElasticClient client, IResponse resp, ISearchRequest? originalRequest)
        {
            // Prefer the actual URL NEST hit; fall back to something sane if missing.
            var url = resp?.ApiCall?.Uri?.ToString() ?? "http://localhost:9200/_search";

            // 1) If NEST captured the body, use it…
            string? body = null;
            var bytes = resp?.ApiCall?.RequestBodyInBytes;
            if (bytes is { Length: > 0 })
                body = System.Text.Encoding.UTF8.GetString(bytes);

            // 2) …otherwise, serialize the original request ourselves.
            if (string.IsNullOrWhiteSpace(body) && originalRequest != null)
            {
                using var ms = new System.IO.MemoryStream();
                client.RequestResponseSerializer.Serialize(originalRequest, ms, SerializationFormatting.Indented);
                body = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }

            // Escape for: curl -d '...'
            static string Esc(string s) => s.Replace("'", "'\"'\"'");

            // Use POST since we’re sending a body (ES accepts POST for _search).
            return $"curl -X POST \"{url}\" -H 'Content-Type: application/json' -d '{Esc(body ?? "{}")}'";
        }



        public Task<IndexResponse> IndexAsync(Movie document) => _elasticClient.IndexDocumentAsync(document);

        public Task<UpdateResponse<Movie>> UpdateAsync(string id, Movie document) =>
            _elasticClient.UpdateAsync<Movie>(id, u => u.Doc(document).DocAsUpsert());

        public Task<DeleteResponse> DeleteAsync(string id) => _elasticClient.DeleteAsync<Movie>(id);

        public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
        {
            var result = await _elasticClient.SearchAsync<Aggregation>(q => q
                .Size(0).Index(IndexName).Aggregations(agg => agg.Terms("distinct", e => e.Field(field).Size(10000))));
            var ret = new List<string>();
            var firstAgg = result.Aggregations.FirstOrDefault().Value as BucketAggregate;
            if (firstAgg?.Items != null)
            {
                foreach (var item in firstAgg.Items)
                {
                    if (item is KeyedBucket<object> kb)
                    {
                        var value = kb.KeyAsString ?? kb.Key?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(value))
                        {
                            ret.Add(value);
                        }
                    }
                }
            }
            return ret;
        }

        public Task<ISearchResponse<Movie>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null)
        {
            return SearchWithRulesetAsync(ruleset, size, from, sort, false);
        }

        public async Task<ISearchResponse<Movie>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null, bool includeDescriptive = false)
        {
            var queryObject = await ConvertRulesetToElasticSearch(ruleset);
            var searchRequest = new SearchRequest<Movie>
            {
                Size = size,
                From = from,
                Query = new QueryContainerDescriptor<Movie>().Raw(queryObject.ToString())
            };
            if (!includeDescriptive)
            {
                searchRequest.Source = new SourceFilter { Excludes = new[] { "synopsis" } };
            }
            if (sort != null && sort.Any())
            {
                searchRequest.Sort = sort;
            }
            else
            {
                searchRequest.Sort = new List<ISort> { new FieldSort { Field = "_id", Order = SortOrder.Ascending } };
            }
            return await SearchAsync(searchRequest);
        }

            
        public async Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr)
        {
            if (!ValidateRuleset(rr))
            {
                throw new ArgumentException("Invalid ruleset", nameof(rr));
            }
            var dto = MapToDto(rr);
            var result = await _bqlService.Ruleset2ElasticSearch(dto);
            return result is JObject jo ? jo : JObject.FromObject(result);
        }

        private static bool ValidateRuleset(Ruleset rr)
        {
            if (rr.rules == null || rr.rules.Count == 0)
            {
                return !string.IsNullOrWhiteSpace(rr.field);
            }
            return rr.rules.All(ValidateRuleset);
        }

        private static RulesetDto MapToDto(Ruleset rr)
        {
            return new RulesetDto
            {
                field = rr.field,
                @operator = rr.@operator,
                value = rr.value,
                condition = rr.condition,
                @not = rr.@not,
                rules = rr.rules?.Select(MapToDto).ToList() ?? new List<RulesetDto>()
            };
        }

        public Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto viewDto, int size = 20, int from = 0, IList<ISort>? sort = null)
        {
            string query = queryObject.ToString();
            var request = new SearchRequest<Movie>("movies")
            {
                Size = 0,
                From = from,
                Query = new QueryContainerDescriptor<Movie>().Raw(query),
                Aggregations = new AggregationDictionary
                {
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
            var uncategorizedRequest = new SearchRequest<Movie>("movies")
            {
                Size = 0,
                From = from,
                Query = new QueryContainerDescriptor<Movie>().Raw(query),
                Aggregations = new AggregationDictionary
                {
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
                                            new ExistsQuery
                                            {
                                                Field = viewDto.Aggregation
                                            }
                                        }
                                    },
                                    new TermQuery
                                    {
                                        Field = viewDto.Aggregation,
                                        Value = string.Empty
                                    }
                                },
                                MinimumShouldMatch = 1
                            }
                        }
                    }
                }
            };
            return SearchUsingViewAsync(request, uncategorizedRequest);
        }

        public async Task<List<ViewResultDto>> SearchUsingViewAsync(ISearchRequest request, ISearchRequest uncategorizedRequest)
        {
            List<ViewResultDto> content = new();
            var result = await _elasticClient.SearchAsync<Aggregation>(request);
            var aggList = result.Aggregations?.ToList();
            if (aggList == null || aggList.Count == 0)
            {
                return content;
            }
            var bucketAggregate = aggList[0].Value as BucketAggregate;
            if (bucketAggregate == null)
            {
                return content;
            }
            foreach (var it in bucketAggregate.Items.OfType<KeyedBucket<object>>())
            {
                string categoryName = it.KeyAsString ?? (it.Key?.ToString() ?? string.Empty);
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
                    Count = it.DocCount,
                    NotCategorized = notCategorized
                });
            }
            content = content.OrderBy(cat => cat.CategoryName).ToList();
            var uncategorizedResponse = await _elasticClient.SearchAsync<Movie>(uncategorizedRequest);
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
