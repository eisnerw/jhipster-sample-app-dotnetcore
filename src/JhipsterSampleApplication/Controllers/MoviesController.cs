#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/movies")]
    public class MoviesController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly IElasticClient _elasticClient;
        private readonly IMovieBqlService _bqlService;
        private readonly IMapper _mapper;
        private readonly ILogger<MoviesController> _logger;

        public MoviesController(IMovieService movieService, IElasticClient elasticClient, IMovieBqlService bqlService, IMapper mapper, ILogger<MoviesController> logger)
        {
            _movieService = movieService;
            _elasticClient = elasticClient;
            _bqlService = bqlService;
            _mapper = mapper;
            _logger = logger;
        }

        public class RawSearchRequestDto
        {
            public string? Query { get; set; }
            public int? From { get; set; }
            public int? Size { get; set; }
            public string? Sort { get; set; }
        }

        [HttpGet("html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById(string id)
        {
            var searchRequest = new SearchRequest<Movie>
            {
                Query = new QueryContainerDescriptor<Movie>().Term(t => t.Field("_id").Value(id))
            };
            var response = await _movieService.SearchAsync(searchRequest, includeDescriptive: true);
            if (!response.IsValid || !response.Documents.Any())
            {
                return NotFound();
            }
            var m = response.Documents.First();
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>")
              .Append(WebUtility.HtmlEncode(m.Title ?? "Movie"))
              .Append("</title></head><body>");
            void AppendField(string label, string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                sb.Append("<div><strong>").Append(WebUtility.HtmlEncode(label)).Append(":</strong> ")
                  .Append(WebUtility.HtmlEncode(value)).Append("</div>");
            }
            AppendField("Title", m.Title);
            AppendField("Release Year", m.ReleaseYear?.ToString());
            AppendField("Genres", m.Genres == null ? null : string.Join(", ", m.Genres));
            AppendField("Runtime", m.RuntimeMinutes?.ToString());
            AppendField("Country", m.Country);
            AppendField("Languages", m.Languages == null ? null : string.Join(", ", m.Languages));
            AppendField("Budget", m.BudgetUsd?.ToString());
            AppendField("Gross", m.GrossUsd?.ToString());
            AppendField("Rotten Tomatoes", m.RottenTomatoesScores?.ToString());
            AppendField("Summary", m.Summary);
            AppendField("Synopsis", m.Synopsis);
            sb.Append("</body></html>");
            return Content(sb.ToString(), "text/html");
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

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<object>), 200)]
        public async Task<IActionResult> SearchWithLuceneQuery([FromQuery] string query, [FromQuery] int from = 0, [FromQuery] int pageSize = 20, [FromQuery] string? sort = null, [FromQuery] bool includeDescriptive = false)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query cannot be empty");
            }
            JObject queryStringObject = new JObject(new JProperty("query", query));
            JObject queryObject = new JObject(new JProperty("query_string", queryStringObject));
            return await Search(queryObject, pageSize, from, sort, includeDescriptive);
        }

        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<object>), 200)]
        public async Task<IActionResult> SearchWithRuleset([FromBody] RulesetDto rulesetDto, [FromQuery] int from = 0, [FromQuery] int pageSize = 20, [FromQuery] string? sort = null, [FromQuery] bool includeDescriptive = false)
        {
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var result = await _movieService.SearchWithRulesetAsync(ruleset, pageSize, from, sort == null ? null : new List<ISort> { new FieldSort { Field = sort } }, includeDescriptive);
            return Ok(new SearchResultDto<object> { TotalHits = result.Total, Hits = result.Documents.Select(d => (object)d).ToList() });
        }

        [HttpPost("search/raw")]
        [ProducesResponseType(typeof(SearchResultDto<object>), 200)]
        public async Task<IActionResult> RawSearch([FromBody] RawSearchRequestDto request, [FromQuery] bool includeDescriptive = false)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("Query cannot be empty");
            }
            var searchRequest = new SearchRequest<Movie>
            {
                From = request.From ?? 0,
                Size = request.Size ?? 20,
                Query = new QueryContainerDescriptor<Movie>().Raw(request.Query)
            };
            if (!string.IsNullOrEmpty(request.Sort))
            {
                searchRequest.Sort = new List<ISort> { new FieldSort { Field = request.Sort } };
            }
            var response = await _movieService.SearchAsync(searchRequest, includeDescriptive);
            return Ok(new SearchResultDto<object> { TotalHits = response.Total, Hits = response.Documents.Select(d => (object)d).ToList() });
        }

        private async Task<IActionResult> Search(JObject queryObject, int pageSize, int from, string? sort, bool includeDescriptive)
        {
            var searchRequest = new SearchRequest<Movie>
            {
                Size = pageSize,
                From = from,
                Query = new QueryContainerDescriptor<Movie>().Raw(queryObject.ToString())
            };
            if (!string.IsNullOrEmpty(sort))
            {
                searchRequest.Sort = new List<ISort> { new FieldSort { Field = sort } };
            }
            var response = await _movieService.SearchAsync(searchRequest, includeDescriptive);
            return Ok(new SearchResultDto<object> { TotalHits = response.Total, Hits = response.Documents.Select(d => (object)d).ToList() });
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MovieDto), 200)]
        public async Task<IActionResult> GetById(string id, [FromQuery] bool includeDescriptive = false)
        {
            var searchRequest = new SearchRequest<Movie>
            {
                Query = new QueryContainerDescriptor<Movie>().Term(t => t.Field("_id").Value(id)),
                Source = includeDescriptive ? null : new SourceFilter { Excludes = new[] { "synopsis" } }
            };
            var response = await _movieService.SearchAsync(searchRequest, includeDescriptive);
            if (!response.IsValid || !response.Documents.Any())
            {
                return NotFound();
            }
            var m = response.Documents.First();
            var dto = new MovieDto
            {
                Id = id,
                Title = m.Title,
                ReleaseYear = m.ReleaseYear,
                Genres = m.Genres,
                RuntimeMinutes = m.RuntimeMinutes,
                Country = m.Country,
                Languages = m.Languages,
                BudgetUsd = m.BudgetUsd,
                GrossUsd = m.GrossUsd,
                RottenTomatoesScores = m.RottenTomatoesScores,
                Summary = m.Summary,
                Synopsis = includeDescriptive ? m.Synopsis : null
            };
            return Ok(dto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Create([FromBody] MovieCreateUpdateDto dto)
        {
            var movie = _mapper.Map<Movie>(dto);
            movie.Id = dto.Id;
            var response = await _movieService.IndexAsync(movie);
            return Ok(new SimpleApiResponse { Success = response.IsValid, Message = response.DebugInformation.Split('\n')[0] });
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Update(string id, [FromBody] MovieCreateUpdateDto dto)
        {
            var movie = _mapper.Map<Movie>(dto);
            movie.Id = id;
            var response = await _movieService.UpdateAsync(id, movie);
            return Ok(new SimpleApiResponse { Success = response.IsValid, Message = response.DebugInformation.Split('\n')[0] });
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _movieService.DeleteAsync(id);
            return Ok(new SimpleApiResponse { Success = response.IsValid, Message = response.DebugInformation.Split('\n')[0] });
        }

        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<IActionResult> GetUniqueFieldValues(string field)
        {
            var esField = field == "release_year" ? field : field + ".keyword";
            var values = await _movieService.GetUniqueFieldValuesAsync(esField);
            return Ok(values);
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

        [HttpPost("bql-to-ruleset")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
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
        public async Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetDto ruleset)
        {
            var bqlQuery = await _bqlService.Ruleset2Bql(ruleset);
            return Ok(bqlQuery);
        }

        [HttpPost("ruleset-to-bql-to-ruleset")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        public async Task<ActionResult<RulesetDto>> ConvertRulesetToBqlToRuleset([FromBody] RulesetDto ruleset)
        {
            var bqlQuery = await _bqlService.Ruleset2Bql(ruleset);
            var roundTrip = await _bqlService.Bql2Ruleset(bqlQuery);
            return Ok(roundTrip);
        }

        [HttpPost("ruleset-to-elasticsearch")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetDto rulesetDto)
        {
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var elasticQuery = await _movieService.ConvertRulesetToElasticSearch(ruleset);
            return Ok(elasticQuery);
        }
    }
}
