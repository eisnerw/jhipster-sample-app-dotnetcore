#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;

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

        [HttpGet("html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById(string id)
        {
            var result = await _entityController.GetById("supreme", id, includeDetails: true) as OkObjectResult;
            if (result == null) return NotFound();
            var s = result.Value as JObject ?? new JObject();

            string? Join(IEnumerable<string>? list)
            {
                if (list == null) return null;
                var vals = list.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => WebUtility.HtmlEncode(v.Trim())).ToList();
                return vals.Count > 0 ? string.Join(", ", vals) : null;
            }

            string? JoinDissent(string? dissent)
            {
                if (string.IsNullOrWhiteSpace(dissent)) return null;
                var parts = dissent.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                return Join(parts);
            }

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><base target=\"_blank\"><title>")
                .Append(WebUtility.HtmlEncode(s.Value<string>("Name") ?? "Supreme"))
                .Append("</title><style>body{margin:0;padding:8px;font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,Helvetica Neue,Arial,\"Apple Color Emoji\",\"Segoe UI Emoji\";font-size:14px;line-height:1.4;color:#111} .empty{color:#666} .field-name{font-weight:600} .field{margin-bottom:0.7em} .inline-field{display:inline-block;margin-right:0.25in}</style></head><body>");

            if (!string.IsNullOrWhiteSpace(s.Value<string>("Name")) || !string.IsNullOrWhiteSpace(s.Value<string>("docket_number")))
            {
                sb.Append("<h3>").Append(WebUtility.HtmlEncode(s.Value<string>("name") ?? string.Empty));
                if (!string.IsNullOrWhiteSpace(s.Value<string>("docket_number")))
                {
                        sb.Append(" (").Append(WebUtility.HtmlEncode(s.Value<string>("docket_number"))).Append(")");
                }
                sb.Append("</h3>");
            }

            string FormatLabel(string label)
            {
                    return string.Join(" ", label.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w =>
                            (w.Equals("of", StringComparison.OrdinalIgnoreCase) || w.Equals("the", StringComparison.OrdinalIgnoreCase))
                                    ? w.ToLowerInvariant()
                                    : w.Equals("url", StringComparison.OrdinalIgnoreCase)
                                            ? "URL"
                                            : char.ToUpperInvariant(w[0]) + w.Substring(1)));
            }

            void AppendField(string label, string? value)
            {
                    if (string.IsNullOrWhiteSpace(value)) return;
                    sb.Append("<div class=\"field\"><span class=\"field-name\">")
                        .Append(WebUtility.HtmlEncode(FormatLabel(label)))
                        .Append(":</span> ")
                        .Append(value)
                        .Append("</div>");
            }

            void AppendInlineFields(params (string label, string? value)[] fields)
            {
                    var items = fields.Where(f => !string.IsNullOrWhiteSpace(f.value)).ToList();
                    if (items.Count == 0) return;
                    sb.Append("<div class=\"field\">");
                    foreach (var item in items)
                    {
                            sb.Append("<span class=\"inline-field\"><span class=\"field-name\">")
                                .Append(WebUtility.HtmlEncode(FormatLabel(item.label)))
                                .Append(":</span> ")
                                .Append(item.value)
                                .Append("</span>");
                    }
                    sb.Append("</div>");
            }

            AppendInlineFields(
                    ("term", WebUtility.HtmlEncode(s.Value<int?>("term")?.ToString())),
                    ("lower court", WebUtility.HtmlEncode(s.Value<string>("lower_court") ?? string.Empty)),
                    ("jurisdiction", WebUtility.HtmlEncode(s.Value<string>("manner_of_jurisdiction") ?? string.Empty)),
                    ("decision", WebUtility.HtmlEncode(s.Value<string>("decision") ?? string.Empty)),
                    ("advocates", Join(s["advocates"]?.ToObject<List<string>>()))
            );
            AppendField("description", WebUtility.HtmlEncode(s.Value<string>("description") ?? string.Empty));
            AppendField("question", WebUtility.HtmlEncode(s.Value<string>("question") ?? string.Empty));
            AppendField("facts of the case", WebUtility.HtmlEncode(s.Value<string>("facts_of_the_case") ?? string.Empty));
            AppendField("conclusion", WebUtility.HtmlEncode(s.Value<string>("conclusion") ?? string.Empty));
            AppendField("opinion", WebUtility.HtmlEncode(s.Value<string>("opinion") ?? string.Empty));
            AppendField("dissent", JoinDissent(s.Value<string>("dissent") ?? string.Empty));
            AppendInlineFields(
                    ("justia url", string.IsNullOrWhiteSpace(s.Value<string>("justia_url")) ? null : "<a href=\"" + WebUtility.HtmlEncode(s.Value<string>("justia_url")) + "\">" + WebUtility.HtmlEncode(s.Value<string>("justia_url")) + "</a>"),
                    ("oyez url", string.IsNullOrWhiteSpace(s.Value<string>("argument2_url")) ? null : "<a href=\"" + WebUtility.HtmlEncode(s.Value<string>("argument2_url")) + "\">" + WebUtility.HtmlEncode(s.Value<string>("argument2_url")) + "</a>")
            );                        
            AppendField("majority", Join(s["majority"]?.ToObject<List<string>>()));
            AppendField("minority", Join(s["minority"]?.ToObject<List<string>>()));
            AppendField("recused", Join(s["recused"]?.ToObject<List<string>>()));

            sb.Append("</body></html>");
            return Content(sb.ToString(), "text/html");
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
