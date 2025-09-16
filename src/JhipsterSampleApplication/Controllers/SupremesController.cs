#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;
using System.Collections.Generic;
using System.Net;

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/supreme")]
    public class SupremesController : ControllerBase
    {
        private readonly EntityController _entityController;

        public SupremesController(EntityController entityController)
        {
            _entityController = entityController;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Create([FromBody] SupremeDto dto)
        {
            var obj = JObject.FromObject(dto);
            return _entityController.Create("supreme", obj);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SupremeDto), 200)]
        public Task<IActionResult> GetById(string id, [FromQuery] bool includeDetails = false)
        {
            return _entityController.GetById("supreme", id, includeDetails);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Update(string id, [FromBody] SupremeDto dto)
        {
            var obj = JObject.FromObject(dto);
            obj["Id"] = id;
            return _entityController.Update("supreme", id, obj);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Delete(string id)
        {
            return _entityController.Delete("supreme", id);
        }

        [HttpPost("search/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResultDto<SupremeDto>), 200)]
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
            return _entityController.SearchWithBql("supreme", bqlQuery, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<SupremeDto>), 200)]
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
            return _entityController.SearchWithRuleset("supreme", rulesetDto, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/elasticsearch")]
        [ProducesResponseType(typeof(SearchResultDto<SupremeDto>), 200)]
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
            return _entityController.Search("supreme", elasticsearchQuery, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<SupremeDto>), 200)]
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
            return _entityController.SearchWithLuceneQuery("supreme", query, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public Task<IActionResult> GetUniqueFieldValues(string field)
        {
            return _entityController.GetUniqueFieldValues("supreme", field);
        }

        [HttpPost("bql-to-ruleset")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public Task<ActionResult<RulesetDto>> ConvertBqlToRuleset([FromBody] string query)
        {
            return _entityController.ConvertBqlToRuleset("supreme", query);
        }

        [HttpPost("ruleset-to-bql")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetDto ruleset)
        {
            return _entityController.ConvertRulesetToBql("supreme", ruleset);
        }

        [HttpPost("ruleset-to-elasticsearch")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetDto rulesetDto)
        {
            return _entityController.ConvertRulesetToElasticSearch("supreme", rulesetDto);
        }

        [HttpPost("categorize")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> Categorize([FromBody] CategorizeRequestDto request)
        {
            return _entityController.Categorize("supreme", request);
        }

        [HttpPost("categorize-multiple")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> CategorizeMultiple([FromBody] CategorizeMultipleRequestDto request)
        {
            return _entityController.CategorizeMultiple("supreme", request);
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
            return _entityController.GetQueryBuilderSpec("supreme");
        }
    }
}
