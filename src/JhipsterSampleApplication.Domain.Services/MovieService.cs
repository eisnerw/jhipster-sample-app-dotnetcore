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
    public class MovieService : EntityService<Movie>, IMovieService
    {
        private readonly IElasticClient _elasticClient;
        private readonly IBqlService<Movie> _bqlService;

        public MovieService(IElasticClient elasticClient, IBqlService<Movie> bqlService, IViewService viewService)
         : base("movies", "synopsis", elasticClient, bqlService, viewService)
        {
            _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
            _bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
        }

        public async Task<ISearchResponse<Movie>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null, bool includeDetails = false)
        {
            var queryObject = await ConvertRulesetToElasticSearch(ruleset);
            var searchRequest = new SearchRequest<Movie>
            {
                Size = size,
                From = from,
                Query = new QueryContainerDescriptor<Movie>().Raw(queryObject.ToString())
            };
            if (!includeDetails)
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
            return await SearchAsync(searchRequest, includeDetails);
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
    }
}
