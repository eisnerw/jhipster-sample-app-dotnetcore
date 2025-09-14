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

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/birthdays")]
    public class BirthdaysController : ControllerBase
    {
        private readonly IEntityService<Birthday> _birthdayService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IBqlService<Birthday> _bqlService;
        private readonly ILogger<BirthdaysController> _log;
        private readonly IMapper _mapper;
        private readonly IViewService _viewService;
        private readonly IHistoryService _historyService;

        public BirthdaysController(
            IServiceProvider serviceProvider,
            INamedQueryService namedQueryService,
            ILogger<BqlService<Birthday>> bqlLogger,
            ILogger<BirthdaysController> logger,
            IMapper mapper,
            IHistoryService historyService,
            IViewService viewService)
        {
            _bqlService = new BqlService<Birthday>(
                bqlLogger,
                namedQueryService,
                BqlService<Birthday>.LoadSpec("birthday"),
                "birthdays");
            _birthdayService = new EntityService<Birthday>("birthdays", "wikipedia", serviceProvider, _bqlService, viewService);
            _serviceProvider = serviceProvider;
            _log = logger;
            _mapper = mapper;
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
            _historyService = historyService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Create([FromBody] BirthdayDto dto)
        {
            var birthday = new Birthday
            {
                Id = dto.Id,
                Lname = dto.Lname,
                Fname = dto.Fname,
                Sign = dto.Sign,
                Dob = dto.Dob,
                IsAlive = dto.IsAlive,
                Text = dto.Text,
                Wikipedia = dto.Wikipedia
            };

            var response = await _birthdayService.IndexAsync(birthday);
            return Ok(new SimpleApiResponse
            {
                Success = response.Success,
                Message = response.Message
            });
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BirthdayDto), 200)]
        public async Task<IActionResult> GetById(string id, [FromQuery] bool includeDetails = false)
        {
            var spec = new SearchSpec<Birthday> { Id = id, IncludeDetails = includeDetails };
            var response = await _birthdayService.SearchAsync(spec);
            if (!response.IsValid || !response.Documents.Any())
            {
                return NotFound();
            }
            var r = response.Documents.First();
            var dto = _mapper.Map<BirthdayDto>(r);
            if (!includeDetails)
            {
                dto.Wikipedia = null;
            }
            return Ok(dto);
        }

        [HttpGet("html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById(string id)
        {
            var spec = new SearchSpec<Birthday> { Id = id, IncludeDetails = true, PitId = "" };
            var response = await _birthdayService.SearchAsync(spec);
            if (!response.IsValid || !response.Documents.Any())
            {
                return NotFound();
            }
            var birthday = response.Documents.First();
            var fullName = ($"{birthday.Fname} {birthday.Lname}").Trim();
            var wikipediaHtml = birthday.Wikipedia ?? string.Empty;
            var title = string.IsNullOrWhiteSpace(fullName) ? "Birthday" : fullName;
            var html = "<!doctype html>" +
                       "<html><head>" +
                       "<meta charset=\"utf-8\">" +
                       "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                       "<base target=\"_blank\">" +
                       "<title>" + WebUtility.HtmlEncode(title) + "</title>" +
                       "<style>body{margin:0;padding:8px;font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,Helvetica Neue,Arial,\"Apple Color Emoji\",\"Segoe UI Emoji\";font-size:14px;line-height:1.4;color:#111} .empty{color:#666}</style>" +
                       "</head><body>" +
                       (string.IsNullOrWhiteSpace(wikipediaHtml)
                           ? ("<div class=\"empty\">No Wikipedia content available." + (string.IsNullOrWhiteSpace(fullName) ? string.Empty : (" for " + WebUtility.HtmlEncode(fullName))) + "</div>")
                           : wikipediaHtml)
                       + "</body></html>";
            return Content(html, "text/html");
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Update(string id, [FromBody] BirthdayDto dto)
        {
            // Check if document exists using the service layer
            var spec = new SearchSpec<Birthday> { Id = id, IncludeDetails = true, PitId = "" };

            var existingResponse = await _birthdayService.SearchAsync(spec);
            if (!existingResponse.IsValid || !existingResponse.Documents.Any())
            {
                return NotFound($"Document with ID {id} not found");
            }
            var birthday = new Birthday
            {
                Id = id,
                Lname = dto.Lname,
                Fname = dto.Fname,
                Sign = dto.Sign,
                Dob = dto.Dob,
                IsAlive = dto.IsAlive,
                Text = dto.Text,
                Wikipedia = dto.Wikipedia,
                Categories = dto.Categories ?? new List<string>()
            };

            var updateResponse = await _birthdayService.UpdateAsync(id, birthday);
            return Ok(new SimpleApiResponse
            {
                Success = updateResponse.Success,
                Message = updateResponse.Message
            });
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _birthdayService.DeleteAsync(id);
            return Ok(new SimpleApiResponse
            {
                Success = response.Success,
                Message = response.Message
            });
        }

        [HttpPost("search/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithBql(
            [FromBody] string bqlQuery,
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
            var queryObject = await _birthdayService.ConvertRulesetToElasticSearch(ruleset);
            await _historyService.Save(new History { User = User?.Identity?.Name, Domain = "birthday", Text = bqlQuery });
            var result = await Search(queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
            return result;
        }

        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
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
            var queryObject = await _birthdayService.ConvertRulesetToElasticSearch(ruleset);
            return await Search(queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/elasticsearch")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
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
                    var viewResult = await _birthdayService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, viewDto, pageSize, from);
                    return Ok(new SearchResultDto<ViewResultDto> { Hits = viewResult, HitType = "view", ViewName = view });
                }
                if (category == "(Uncategorized)")
                {
                    var baseField = (viewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
                    var missingFilter = new JObject(
                        new JProperty("bool", new JObject(
                            new JProperty("should", new JArray(
                                new JObject(
                                    new JProperty("bool", new JObject(
                                        new JProperty("must_not", new JArray(
                                            new JObject(
                                                new JProperty("exists", new JObject(
                                                    new JProperty("field", baseField)
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
                        var viewSecondaryResult = await _birthdayService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, secondaryViewDto, pageSize, from);
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
                                                        new JProperty("field", secondaryBaseField)
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
            var sortDescriptor = new List<SortSpec>();
            if (!string.IsNullOrEmpty(sort))
            {
                var sortParts = sort.Contains(',') ? sort.Split(',') : sort.Split(':');
                if (sortParts.Length == 2)
                {
                    var field = sortParts[0];
                    var order = sortParts[1].ToLower();
                    sortDescriptor.Add(new SortSpec { Field = field, Order = order });
                }
            }
            var spec2 = new SearchSpec<Birthday>
            {
                Size = pageSize,
                From = from,
                RawQuery = elasticsearchQuery,
                IncludeDetails = includeDetails,
                PitId = pitId,
                SearchAfter = searchAfter?.Cast<object>().ToList(),
                Sorts = sortDescriptor
            };

            var response = await _birthdayService.SearchAsync(spec2);
            var birthdayDtos = new List<BirthdayDto>();
            foreach (var hit in response.Hits)
            {
                var b = hit.Source;
                var dto = new BirthdayDto
                {
                    Id = b.Id!,
                    Lname = b.Lname,
                    Fname = b.Fname,
                    Sign = b.Sign,
                    Dob = b.Dob,
                    IsAlive = b.IsAlive,
                    Text = b.Text,
                    Wikipedia = includeDetails ? b.Wikipedia : null,
                    Categories = b.Categories
                };
                if (dto.Categories != null && dto.Categories.Count > 0)
                {
                    dto.Categories = dto.Categories
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(s => s.Length == 1 ? s.ToUpperInvariant() : s)
                        .ToList();
                }
                birthdayDtos.Add(dto);
            }
            List<object>? searchAfterResponse = response.Hits.Count > 0 ? response.Hits.Last().Sorts.ToList() : null;
            var hitType = isHitFromViewDrilldown ? "hit" : "birthday";
            return Ok(new SearchResultDto<BirthdayDto>
            {
                Hits = birthdayDtos,
                TotalHits = response.Total,
                HitType = hitType,
                PitId = response.PointInTimeId,
                searchAfter = searchAfterResponse
            });
        }

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
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
            var values = await _birthdayService.GetUniqueFieldValuesAsync(field+".keyword");
            return Ok(values);
        }

        [HttpGet("query-builder-spec")]
        [Produces("application/json")]
        public IActionResult GetQueryBuilderSpec()
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "query-builder", "birthday-qb-spec.json");
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
            var elasticQuery = await _birthdayService.ConvertRulesetToElasticSearch(ruleset);
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
            var result = await _birthdayService.CategorizeAsync(request);
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
            var result = await _birthdayService.CategorizeMultipleAsync(request);
            return Ok(result);
        }

        [HttpGet("health")]
        [ProducesResponseType(typeof(ClusterHealthDto), 200)]
        public async Task<IActionResult> GetHealth()
        {
            var dto = await _birthdayService.GetHealthAsync();
            return Ok(dto);
        }
    }
}
