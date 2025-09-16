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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/birthdays")]
    public class BirthdaysController : ControllerBase
    {
        private readonly EntityController _entityController;

        public BirthdaysController(EntityController entityController)
        {
            _entityController = entityController;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Create([FromBody] BirthdayDto dto)
        {
            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var obj = JObject.FromObject(dto, JsonSerializer.Create(settings));
            return _entityController.Create("birthday", obj);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BirthdayDto), 200)]
        public Task<IActionResult> GetById(string id, [FromQuery] bool includeDetails = false)
        {
            return _entityController.GetById("birthday", id, includeDetails);
        }

        [HttpGet("html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById(string id)
        {
            // Delegate to entity search and format HTML using spec HtmlTemplate later if needed
            var result = await _entityController.GetById("birthday", id, includeDetails: true) as OkObjectResult;
            if (result == null) return NotFound();
            var birthday = result.Value as JObject ?? new JObject();
            var fullName = ($"{birthday["fname"]} {birthday["lname"]}").Trim();
            var wikipediaHtml = birthday.Value<string>("wikipedia") ?? string.Empty;
            var title = string.IsNullOrWhiteSpace(fullName) ? "Birthday" : fullName;
            var html = "<!doctype html>" +
                       "<html><head>" +
                       "<meta charset=\"utf-8\">" +
                       "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                       "<base target=\"_blank\">" +
                       "<title>" + WebUtility.HtmlEncode(title) + "</title>" +
                       "<style>body{margin:0;padding:8px;font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,Helvetica Neue,Arial,\"Apple Color Emoji\",\"Segoe UI Emoji\";font-size:14px;line-height:1.4;color:#111} .empty{color:#666}</style>" +
                       "</head><body>" +
                       (string.IsNullOrWhiteSpace(wikipediaHtml) ? ("<div class=\"empty\">No Wikipedia content available." + (string.IsNullOrWhiteSpace(fullName) ? string.Empty : (" for " + WebUtility.HtmlEncode(fullName))) + "</div>") : wikipediaHtml)
                       + "</body></html>";
            return Content(html, "text/html");
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Update(string id, [FromBody] BirthdayDto dto)
        {
            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var obj = JObject.FromObject(dto, JsonSerializer.Create(settings));
            obj["id"] = id;
            return _entityController.Update("birthday", id, obj);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public Task<IActionResult> Delete(string id)
        {
            var deleteReq = new CategorizeRequestDto { Ids = new List<string> { id }, Category = "__DELETE__", RemoveCategory = true };
            // Use EntityController Delete equivalent by calling Delete endpoint directly is not set; fallback by updating service
            return _entityController.Delete("birthday", id);
        }

        [HttpPost("search/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
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
            return _entityController.SearchWithBql("birthday", bqlQuery, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
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
            return _entityController.SearchWithRuleset("birthday", rulesetDto, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/elasticsearch")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
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
            return _entityController.Search("birthday", elasticsearchQuery, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
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
            return _entityController.SearchWithLuceneQuery("birthday", query, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public Task<IActionResult> GetUniqueFieldValues(string field)
        {
            return _entityController.GetUniqueFieldValues("birthday", field);
        }

        [HttpPost("bql-to-ruleset")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public Task<ActionResult<RulesetDto>> ConvertBqlToRuleset([FromBody] string query)
        {
            return _entityController.ConvertBqlToRuleset("birthday", query);
        }

        [HttpPost("ruleset-to-bql")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetDto ruleset)
        {
            return _entityController.ConvertRulesetToBql("birthday", ruleset);
        }

        [HttpPost("ruleset-to-elasticsearch")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetDto rulesetDto)
        {
            return _entityController.ConvertRulesetToElasticSearch("birthday", rulesetDto);
        }

        [HttpPost("categorize")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> Categorize([FromBody] CategorizeRequestDto request)
        {
            return _entityController.Categorize("birthday", request);
        }

        [HttpPost("categorize-multiple")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public Task<IActionResult> CategorizeMultiple([FromBody] CategorizeMultipleRequestDto request)
        {
            return _entityController.CategorizeMultiple("birthday", request);
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
            return _entityController.GetQueryBuilderSpec("birthday");
        }
    }
}
