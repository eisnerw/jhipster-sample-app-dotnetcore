#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using JhipsterSampleApplication.Domain.Search;
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
        private readonly EntityController _entityController;

        public MoviesController(EntityController entityController)
        {
            _entityController = entityController;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Create([FromBody] JObject document)
        {
            return _entityController.Create("movie", document);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(JObject), 200)]
        public Task<IActionResult> GetById(string id, [FromQuery] bool includeDetails = false)
        {
            return _entityController.GetById("movie", id, includeDetails);
        }

        [HttpGet("html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById(string id)
        {
            return await _entityController.GetHtmlById("movie", id);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Update(string id, [FromBody] JObject document)
        {
            return _entityController.Update("movie", id, document);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Delete(string id)
        {
            return _entityController.Delete("movie", id);
        }

        [HttpPost("search/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> SearchWithBql([FromBody] string bqlQuery,
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
            // overriding sort (for now)
            return _entityController.SearchWithBql("movie", bqlQuery, view, category, secondaryCategory, includeDetails, from, pageSize, "title"/*sort*/, pitId, searchAfter);
        }

        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> SearchWithRuleset([FromBody] RulesetDto rulesetDto,
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
            return _entityController.SearchWithRuleset("movie", rulesetDto, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/elasticsearch")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> Search([FromBody] JObject elasticsearchQuery,
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
            return _entityController.Search("movie", elasticsearchQuery, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        public Task<IActionResult> SearchWithLuceneQuery(
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
            return _entityController.SearchWithLuceneQuery("movie", query, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public Task<IActionResult> GetUniqueFieldValues(string field)
        {
            return _entityController.GetUniqueFieldValues("movie", field);
        }

        [HttpPost("bql-to-ruleset")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public Task<ActionResult<RulesetDto>> ConvertBqlToRuleset([FromBody] string query)
        {
            return _entityController.ConvertBqlToRuleset("movie", query);
        }

        [HttpPost("ruleset-to-bql")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetDto ruleset)
        {
            return _entityController.ConvertRulesetToBql("movie", ruleset);
        }

        [HttpPost("ruleset-to-elasticsearch")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetDto rulesetDto)
        {
            return _entityController.ConvertRulesetToElasticSearch("movie", rulesetDto);
        }

        [HttpPost("categorize")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> Categorize([FromBody] CategorizeRequestDto request)
        {
            return _entityController.Categorize("movie", request);
        }

        [HttpPost("categorize-multiple")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> CategorizeMultiple([FromBody] CategorizeMultipleRequestDto request)
        {
            return _entityController.CategorizeMultiple("movie", request);
        }

        [HttpGet("health")]
        [ProducesResponseType(typeof(ClusterHealthDto), 200)]
        public Task<IActionResult> GetHealth()
        {
            return _entityController.GetHealth();
        }

        [HttpGet("query-builder-spec")]
        [Produces("application/json")]
        public IActionResult GetQueryBuilderSpec()
        {
            return _entityController.GetQueryBuilderSpec("movie");
        }
    }
}
