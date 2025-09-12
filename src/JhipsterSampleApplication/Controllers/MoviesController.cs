#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nest;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using JhipsterSampleApplication.Dto;
using AutoMapper;
using Microsoft.Extensions.Logging;
using System.Text;

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/movies")]
    public class MoviesController : ControllerBase
    {
        private readonly IEntityService<Movie> _movieService;
        private readonly IElasticClient _elasticClient;
        private readonly IBqlService<Movie> _bqlService;
        private readonly ILogger<MoviesController> _log;
        private readonly IMapper _mapper;
        private readonly IViewService _viewService;
        private readonly IHistoryService _historyService;

        public MoviesController(
            IElasticClient elasticClient,
            INamedQueryService namedQueryService,
            ILogger<BqlService<Movie>> bqlLogger,
            ILogger<MoviesController> logger,
            IMapper mapper,
            IHistoryService historyService,
            IViewService viewService)
        {
            _bqlService = new BqlService<Movie>(
                bqlLogger,
                namedQueryService,
                BqlService<Movie>.LoadSpec("movie"),
                "movies");
            _movieService = new EntityService<Movie>("movies", "synopsis", elasticClient, _bqlService, viewService);
            _elasticClient = elasticClient;
            _log = logger;
            _mapper = mapper;
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _historyService = historyService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Create([FromBody] MovieDto dto)
        {
            var movie = _mapper.Map<Movie>(dto);
            movie.Id = dto.Id;
            var response = await _movieService.IndexAsync(movie);
            return Ok(new SimpleApiResponse
            {
                Success = response.IsValid,
                Message = response.DebugInformation.Split('\n')[0]
            });
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MovieDto), 200)]
        public async Task<IActionResult> GetById(string id, [FromQuery] bool includeDetails = false)
        {
            var searchRequest = new SearchRequest<Movie>
            {
                Query = new QueryContainerDescriptor<Movie>().Term(t => t.Field("_id").Value(id)),
                Source = includeDetails ? null : new SourceFilter
                {
                    Excludes = new[] { "synopsis" }
                }
            };
            var response = await _movieService.SearchAsync(searchRequest, includeDetails);
            if (!response.IsValid || !response.Documents.Any())
            {
                return NotFound();
            }
            var r = response.Documents.First();
            var dto = _mapper.Map<MovieDto>(r);
            if (!includeDetails)
            {
                dto.Synopsis = null;
            }
            return Ok(dto);
        }

        [HttpGet("html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById(string id)
        {
            var searchRequest = new SearchRequest<Movie>
            {
                Query = new QueryContainerDescriptor<Movie>().Term(t => t.Field("_id").Value(id))
            };
            var response = await _movieService.SearchAsync(searchRequest, includeDetails: true);
            if (!response.IsValid || !response.Documents.Any())
            {
                return NotFound();
            }
            var m = response.Documents.First();

            string? Join(IEnumerable<string>? list)
            {
                if (list == null) return null;
                var vals = list.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => WebUtility.HtmlEncode(v.Trim())).ToList();
                return vals.Count > 0 ? string.Join(", ", vals) : null;
            }

            string? Encode(string? v) => string.IsNullOrWhiteSpace(v) ? null : WebUtility.HtmlEncode(v);

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><base target=\"_blank\"><title>")
              .Append(WebUtility.HtmlEncode(m.Title ?? "Movie"))
              .Append("</title><style>body{margin:0;padding:8px;font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,Helvetica Neue,Arial,\"Apple Color Emoji\",\"Segoe UI Emoji\";font-size:14px;line-height:1.4;color:#111}.field-name{font-weight:600}.field{margin-bottom:0.7em}</style></head><body>");

            void AppendField(string label, string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                sb.Append("<div class=\"field\"><span class=\"field-name\">")
                  .Append(WebUtility.HtmlEncode(label))
                  .Append(":</span> ")
                  .Append(value)
                  .Append("</div>");
            }

            AppendField("Title", Encode(m.Title));
            AppendField("Release Year", Encode(m.ReleaseYear?.ToString()));
            AppendField("Genres", Join(m.Genres));
            AppendField("Runtime", Encode(m.RuntimeMinutes?.ToString()));
            AppendField("Country", Encode(m.Country));
            AppendField("Languages", Join(m.Languages));
            AppendField("Directors", Join(m.Directors));
            AppendField("Producers", Join(m.Producers));
            AppendField("Writers", Join(m.Writers));
            AppendField("Cast", Join(m.Cast));
            AppendField("Budget", Encode(m.BudgetUsd?.ToString()));
            AppendField("Gross", Encode(m.GrossUsd?.ToString()));
            AppendField("Rotten Tomatoes", Encode(m.RottenTomatoesScore?.ToString()));
            AppendField("Summary", Encode(m.Summary));
            AppendField("Synopsis", Encode(m.Synopsis));

            sb.Append("</body></html>");
            return Content(sb.ToString(), "text/html");
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Update(string id, [FromBody] MovieDto dto)
        {
            var searchRequest = new SearchRequest<Movie>
            {
                Query = new QueryContainerDescriptor<Movie>().Term(t => t.Field("_id").Value(id))
            };

            var existingResponse = await _movieService.SearchAsync(searchRequest, includeDetails: true, "");
            if (!existingResponse.IsValid || !existingResponse.Documents.Any())
            {
                return NotFound($"Document with ID {id} not found");
            }
            var movie = _mapper.Map<Movie>(dto);
            movie.Id = id;
            var updateResponse = await _movieService.UpdateAsync(id, movie);
            return Ok(new SimpleApiResponse
            {
                Success = updateResponse.IsValid,
                Message = updateResponse.DebugInformation.Split('\n')[0]
            });
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _movieService.DeleteAsync(id);
            return Ok(new SimpleApiResponse
            {
                Success = response.IsValid,
                Message = response.DebugInformation.Split('\n')[0]
            });
        }

        [HttpPost("search/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResultDto<MovieDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithBql(
            [FromQuery] string bqlQuery,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false,
            [FromQuery] int from = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                return BadRequest("Query cannot be empty");
            }
            var rulesetDto = await _bqlService.Bql2Ruleset(bqlQuery.Trim());
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var queryObject = await _movieService.ConvertRulesetToElasticSearch(ruleset);
            await _historyService.Save(new History { User = User?.Identity?.Name, Domain = "movie", Text = bqlQuery });
            var result = await Search(queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
            return Ok(result);
        }

        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<MovieDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithRuleset(
            [FromBody] RulesetDto rulesetDto,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false,
            [FromQuery] int from = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null)
        {
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var queryObject = await _movieService.ConvertRulesetToElasticSearch(ruleset);
            return await Search(queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/elasticsearch")]
        [ProducesResponseType(typeof(SearchResultDto<MovieDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Search(
            [FromBody] JObject elasticsearchQuery,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false,
            [FromQuery] int from = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null)
        {
            bool isHitFromViewDrilldown = false;
            if (!string.IsNullOrEmpty(view))
            {
                var viewDto = await _viewService.GetByIdAsync(view);
                if (viewDto == null)
                {
                    throw new ArgumentException($"view '{view}' not found");
                }
                if (category == null)
                {
                    if (secondaryCategory != null)
                    {
                        throw new ArgumentException($"secondaryCategory '{secondaryCategory}' should be null because category is null");
                    }
                    var viewResult = await _movieService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, viewDto, pageSize, from);
                    return Ok(new SearchResultDto<ViewResultDto> { Hits = viewResult, HitType = "view", ViewName = view });
                }
                if (category == "(Uncategorized)")
                {
                    var missingFilter = new JObject(
                        new JProperty("bool", new JObject(
                            new JProperty("should", new JArray(
                                new JObject(
                                    new JProperty("bool", new JObject(
                                        new JProperty("must_not", new JArray(
                                            new JObject(
                                                new JProperty("exists", new JObject(
                                                    new JProperty("field", viewDto.Aggregation)
                                                ))
                                            )
                                        ))
                                    ))
                                ),
                                new JObject(
                                    new JProperty("term", new JObject(
                                        new JProperty(viewDto.Aggregation ?? string.Empty, "")
                                    ))
                                )
                            )),
                            new JProperty("minimum_should_match", 1)
                        ))
                    );
                    elasticsearchQuery = new JObject(
                        new JProperty("bool", new JObject(
                            new JProperty("must", new JArray(
                                missingFilter,
                                elasticsearchQuery
                            ))
                        ))
                    );
                }
                else
                {
                    string categoryQuery = string.IsNullOrEmpty(viewDto.CategoryQuery) ? $"{viewDto.Aggregation}:\"{category}\"" : viewDto.CategoryQuery.Replace("{}", category);
                    elasticsearchQuery = new JObject(
                        new JProperty("bool", new JObject(
                            new JProperty("must", new JArray(
                                new JObject(
                                    new JProperty("query_string", new JObject(
                                        new JProperty("query", categoryQuery)
                                    ))
                                ),
                                elasticsearchQuery
                            ))
                        ))
                    );
                }
                var secondaryViewDto = await _viewService.GetChildByParentIdAsync(view);
                if (secondaryViewDto != null)
                {
                    if (secondaryCategory == null)
                    {
                        var viewSecondaryResult = await _movieService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, secondaryViewDto, from, pageSize);
                        return Ok(new SearchResultDto<ViewResultDto> { Hits = viewSecondaryResult, HitType = "view", ViewName = view, viewCategory = category });
                    }
                    if (secondaryCategory == "(Uncategorized)")
                    {
                        isHitFromViewDrilldown = true;
                        var secondaryBaseField = (secondaryViewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
                        var secondaryMissing = new JObject(
                            new JProperty("bool", new JObject(
                                new JProperty("should", new JArray(
                                    new JObject(
                                        new JProperty("bool", new JObject(
                                            new JProperty("must_not", new JArray(
                                                new JObject(
                                                    new JProperty("exists", new JObject(
                                                        new JProperty("field", secondaryViewDto.Aggregation)
                                                    ))
                                                )
                                            ))
                                        ))
                                    ),
                                    new JObject(
                                        new JProperty("term", new JObject(
                                            new JProperty(secondaryViewDto.Aggregation ?? string.Empty, "")
                                        ))
                                    )
                                )),
                                new JProperty("minimum_should_match", 1)
                            ))
                        );
                        elasticsearchQuery = new JObject(
                            new JProperty("bool", new JObject(
                                new JProperty("must", new JArray(
                                    secondaryMissing,
                                    elasticsearchQuery
                                ))
                            ))
                        );
                    }
                    else
                    {
                        isHitFromViewDrilldown = true;
                        string secondaryCategoryQuery = string.IsNullOrEmpty(secondaryViewDto.CategoryQuery) ? $"{secondaryViewDto.Aggregation}:\"{secondaryCategory}\"" : secondaryViewDto.CategoryQuery.Replace("{}", secondaryCategory);
                        elasticsearchQuery = new JObject(
                            new JProperty("bool", new JObject(
                                new JProperty("must", new JArray(
                                    new JObject(
                                        new JProperty("query_string", new JObject(
                                            new JProperty("query", secondaryCategoryQuery)
                                        ))
                                    ),
                                    elasticsearchQuery
                                ))
                            ))
                        );
                    }
                }
            }
            var searchRequest = new SearchRequest<Movie>
            {
                Size = pageSize,
                From = from
            };

            if (searchAfter != null && searchAfter.Length > 0)
            {
                searchRequest.SearchAfter = searchAfter.Cast<object>().ToList();
                searchRequest.From = null;
            }

            var sortDescriptor = new List<ISort>();
            if (!string.IsNullOrEmpty(sort))
            {
                var sortParts = sort.Contains(',') ? sort.Split(',') : sort.Split(':');
                if (sortParts.Length == 2)
                {
                    var field = sortParts[0];
                    var order = sortParts[1].ToLower() == "desc" ? SortOrder.Descending : SortOrder.Ascending;
                    sortDescriptor.Add(new FieldSort { Field = field, Order = order });
                }
            }
            sortDescriptor.Add(new FieldSort { Field = "_id", Order = SortOrder.Ascending });

            searchRequest.Query = new QueryContainerDescriptor<Movie>().Raw(elasticsearchQuery.ToString());
            searchRequest.Sort = sortDescriptor;

            var response = await _movieService.SearchAsync(searchRequest, includeDetails, pitId);
            var movieDtos = new List<MovieDto>();
            foreach (var hit in response.Hits)
            {
                var m = hit.Source;
                var dto = _mapper.Map<MovieDto>(m);
                if (!includeDetails)
                {
                    dto.Synopsis = null;
                }
                movieDtos.Add(dto);
            }
            List<object>? searchAfterResponse = response.Hits.Count > 0 ? response.Hits.Last().Sorts.ToList() : null;
            var hitType = isHitFromViewDrilldown ? "hit" : "movie";
            return Ok(new SearchResultDto<MovieDto>
            {
                Hits = movieDtos,
                TotalHits = response.Total,
                HitType = hitType,
                PitId = searchRequest.PointInTime?.Id,
                searchAfter = searchAfterResponse
            });
        }

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<MovieDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        public async Task<IActionResult> SearchWithLuceneQuery(
            [FromQuery] string query,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false,
            [FromQuery] int from = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query cannot be empty");
            }
            JObject queryStringObject = new JObject(
                new JProperty("query", query)
            );
            JObject queryObject = new JObject(
                new JProperty("query_string", queryStringObject)
            );
            return await Search(queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<IActionResult> GetUniqueFieldValues(string field)
        {
             var values = await _movieService.GetUniqueFieldValuesAsync(field+".keyword");
            return Ok(values);
        }

        [HttpGet("query-builder-spec")]
        [Produces("application/json")]
        public IActionResult GetQueryBuilderSpec()
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "query-builder", "movie-qb-spec.json");
            if (!System.IO.File.Exists(path))
            {
                return NotFound("Spec file not found");
            }
            var json = System.IO.File.ReadAllText(path);
            return Content(json, "application/json");
        }

        [HttpPost("bql-to-ruleset")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public async Task<ActionResult<RulesetDto>> ConvertBqlToRuleset([FromBody] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query cannot be empty");
            }
            var ruleset = await _bqlService.Bql2Ruleset(query.Trim());
            return Ok(ruleset);
        }

        [HttpPost("ruleset-to-bql")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetDto ruleset)
        {
            var bqlQuery = await _bqlService.Ruleset2Bql(ruleset);
            return Ok(bqlQuery);
        }

        [HttpPost("ruleset-to-elasticsearch")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetDto rulesetDto)
        {
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var elasticQuery = await _movieService.ConvertRulesetToElasticSearch(ruleset);
            return Ok(elasticQuery);
        }

        [HttpPost("categorize")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Categorize([FromBody] CategorizeRequestDto request)
        {
            if (request.Ids == null || !request.Ids.Any())
            {
                return BadRequest("At least one ID must be provided");
            }

            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return BadRequest("Category cannot be empty");
            }
            var result = await _movieService.CategorizeAsync(request);
            return Ok(result);
        }

        [HttpPost("categorize-multiple")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CategorizeMultiple([FromBody] CategorizeMultipleRequestDto request)
        {
            if (request.Rows == null || !request.Rows.Any())
            {
                return BadRequest("At least one row ID must be provided");
            }
            var result = await _movieService.CategorizeMultipleAsync(request);
            return Ok(result);
        }

        [HttpGet("health")]
        [ProducesResponseType(typeof(ClusterHealthDto), 200)]
        public async Task<IActionResult> GetHealth()
        {
            var res = await _elasticClient.Cluster.HealthAsync();
            var dto = new ClusterHealthDto
            {
                Status = res.Status.ToString(),
                NumberOfNodes = res.NumberOfNodes,
                NumberOfDataNodes = res.NumberOfDataNodes,
                ActiveShards = res.ActiveShards,
                ActivePrimaryShards = res.ActivePrimaryShards
            };
            return Ok(dto);
        }
    }
}
