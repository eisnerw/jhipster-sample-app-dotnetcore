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
            var response = await _elasticClient.LowLevel.SearchAsync<SearchResponse<Movie>>(IndexName, PostData.Serializable(request));
            if (!response.IsValid)
            {
                StringResponse retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(IndexName, PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
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

                public Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto viewDto, int size = 20, int from = 0, IList<ISort>? sort = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<ViewResultDto>> SearchUsingViewAsync(ISearchRequest request, ISearchRequest uncategorizedRequest)
        {
            throw new NotImplementedException();
        }

        private static string ToEsField(string? field)
        {
            return field switch
            {
                "title" => "title",
                "release_year" => "release_year",
                "genres" => "genres",
                "runtime_minutes" => "runtime_minutes",
                "country" => "country",
                "languages" => "languages",
                "directors" => "directors",
                "producers" => "producers",
                "writers" => "writers",
                "cast" => "cast",
                "budget_usd" => "budget_usd",
                "gross_usd" => "gross_usd",
                "rotten_tomatoes_scores" => "rotten_tomatoes_scores",
                "summary" => "summary",
                "synopsis" => "synopsis",
                _ => field ?? string.Empty
            };
        }
    }
}
