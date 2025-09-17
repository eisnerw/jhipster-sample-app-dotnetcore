#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Search;
using JhipsterSampleApplication.Dto;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/entity")]
    public class EntityController : ControllerBase
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly IConfiguration _configuration;
        private readonly IEntitySpecRegistry _specRegistry;
        private readonly IViewService _viewService;
        private readonly INamedQueryService _namedQueryService;
        private readonly JhipsterSampleApplication.Domain.Services.EntityService _entityService;

        public EntityController(ElasticsearchClient elasticClient, IConfiguration configuration, IEntitySpecRegistry specRegistry, IViewService viewService, INamedQueryService namedQueryService, JhipsterSampleApplication.Domain.Services.EntityService jsonService)
        {
            _elasticClient = elasticClient;
            _configuration = configuration;
            _specRegistry = specRegistry;
            _viewService = viewService;
            _namedQueryService = namedQueryService;
            _entityService = jsonService;
        }

        private (string Index, string[] DetailFields, string IdField) GetEntityConfig(string entity)
        {
            if (!_specRegistry.TryGetString(entity, "elasticsearchIndex", out var index)
                && !_specRegistry.TryGetString(entity, "elasticSearchIndex", out index)
                && !_specRegistry.TryGetString(entity, "index", out index))
                throw new ArgumentException($"Unknown entity '{entity}'", nameof(entity));

            if (!_specRegistry.TryGetStringArray(entity, "detailFields", out var details))
                _specRegistry.TryGetStringArray(entity, "descriptiveFields", out details);

            var idField = "Id";
            if (_specRegistry.TryGetString(entity, "idField", out var id) && !string.IsNullOrWhiteSpace(id))
                idField = id;

            return (index, details, idField);
        }

        [HttpPost("{entity}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Create([FromRoute] string entity, [FromBody] JObject document)
        {
            var wr = await _entityService.IndexAsync(entity, document);
            return Ok(new SimpleApiResponse { Success = wr.Success, Message = wr.Message });
        }

        [HttpGet("{entity}/{id}")]
        [ProducesResponseType(typeof(JObject), 200)]
        public async Task<IActionResult> GetById([FromRoute] string entity, [FromRoute] string id, [FromQuery] bool includeDetails = false)
        {
            var spec = new SearchSpec<JObject> { Id = id, IncludeDetails = includeDetails };
            var response = await _entityService.SearchAsync(entity, spec);
            if (!response.IsValid || !response.Documents.Any()) return NotFound();
            var obj = response.Documents.First();
            if (!includeDetails)
            {
                obj.Remove("Wikipedia");
                obj.Remove("Synopsis");
            }
            return Ok(obj);
        }

        [HttpPost("search/{entity}/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithBql([FromRoute] string entity, [FromBody] string bqlQuery,
            [FromQuery] string? view = null, [FromQuery] string? category = null, [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false, [FromQuery] int from = 0, [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null, [FromQuery] string? pitId = null, [FromQuery] string[]? searchAfter = null)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery)) return BadRequest("Query cannot be empty");
            var rulesetDto = await new BqlService<object>(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BqlService<object>>(), _namedQueryService, BqlService<object>.LoadSpec(entity), entity).Bql2Ruleset(bqlQuery.Trim());
            var ruleset = new Ruleset { field = rulesetDto.field, @operator = rulesetDto.@operator, value = rulesetDto.value, condition = rulesetDto.condition, not = rulesetDto.not, rules = rulesetDto.rules?.Select(r => new Ruleset { field = r.field, @operator = r.@operator, value = r.value, condition = r.condition, not = r.not, rules = new List<Ruleset>() }).ToList() ?? new List<Ruleset>() };
            var queryObject = await _entityService.ConvertRulesetToElasticSearch(entity, ruleset);
            return await Search(entity, queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/{entity}/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithRuleset([FromRoute] string entity, [FromBody] RulesetDto rulesetDto,
            [FromQuery] string? view = null, [FromQuery] string? category = null, [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false, [FromQuery] int from = 0, [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null, [FromQuery] string? pitId = null, [FromQuery] string[]? searchAfter = null)
        {
            var ruleset = new Ruleset { field = rulesetDto.field, @operator = rulesetDto.@operator, value = rulesetDto.value, condition = rulesetDto.condition, not = rulesetDto.not, rules = rulesetDto.rules?.Select(r => new Ruleset { field = r.field, @operator = r.@operator, value = r.value, condition = r.condition, not = r.not, rules = new List<Ruleset>() }).ToList() ?? new List<Ruleset>() };
            var queryObject = await _entityService.ConvertRulesetToElasticSearch(entity, ruleset);
            return await Search(entity, queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("search/{entity}/elasticsearch")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Search([FromRoute] string entity, [FromBody] JObject elasticsearchQuery,
            [FromQuery] string? view = null, [FromQuery] string? category = null, [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false, [FromQuery] int from = 0, [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null, [FromQuery] string? pitId = null, [FromQuery] string[]? searchAfter = null)
        {
            bool isHitFromViewDrilldown = false;
            if (!string.IsNullOrEmpty(view))
            {
                var viewDto = await _viewService.GetByIdAsync(view);
                if (viewDto == null) throw new ArgumentException($"view '{view}' not found");
                if (category == null)
                {
                    if (secondaryCategory != null) throw new ArgumentException($"secondaryCategory '{secondaryCategory}' should be null because category is null");
                    var viewResult = await SearchWithElasticQueryAndViewAsync(entity, elasticsearchQuery, viewDto, pageSize, from);
                    return Ok(new SearchResultDto<ViewResultDto> { Hits = viewResult, HitType = "view", ViewName = view });
                }
                if (category == "(Uncategorized)")
                {
                    var baseField = (viewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
                    var missingFilter = new JObject
                    {
                        ["bool"] = new JObject
                        {
                            ["should"] = new JArray
                            {
                                new JObject { ["bool"] = new JObject { ["must_not"] = new JArray { new JObject { ["exists"] = new JObject { ["field"] = baseField } } } } },
                                new JObject { ["term"] = new JObject { [viewDto.Aggregation ?? string.Empty] = "" } }
                            },
                            ["minimum_should_match"] = 1
                        }
                    };
                    elasticsearchQuery = new JObject
                    {
                        ["bool"] = new JObject
                        {
                            ["must"] = new JArray { missingFilter, elasticsearchQuery }
                        }
                    };
                }
                else
                {
                    string categoryQuery = string.IsNullOrEmpty(viewDto.CategoryQuery) ? $"{viewDto.Aggregation}:\"{category}\"" : viewDto.CategoryQuery.Replace("{}", category);
                    elasticsearchQuery = new JObject
                    {
                        ["bool"] = new JObject
                        {
                            ["must"] = new JArray
                            {
                                new JObject { ["query_string"] = new JObject { ["query"] = categoryQuery } },
                                elasticsearchQuery
                            }
                        }
                    };
                }
                var secondaryViewDto = await _viewService.GetChildByParentIdAsync(view);
                if (secondaryViewDto != null)
                {
                    if (secondaryCategory == null)
                    {
                        var viewSecondaryResult = await SearchWithElasticQueryAndViewAsync(entity, elasticsearchQuery, secondaryViewDto, pageSize, from);
                        return Ok(new SearchResultDto<ViewResultDto> { Hits = viewSecondaryResult, HitType = "view", ViewName = view, viewCategory = category });
                    }
                    if (secondaryCategory == "(Uncategorized)")
                    {
                        isHitFromViewDrilldown = true;
                        var secondaryBaseField = (secondaryViewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
                        var secondaryMissing = new JObject
                        {
                            ["bool"] = new JObject
                            {
                                ["should"] = new JArray
                                {
                                    new JObject { ["bool"] = new JObject { ["must_not"] = new JArray { new JObject { ["exists"] = new JObject { ["field"] = secondaryBaseField } } } } },
                                    new JObject { ["term"] = new JObject { [secondaryViewDto.Aggregation ?? string.Empty] = "" } }
                                },
                                ["minimum_should_match"] = 1
                            }
                        };
                        elasticsearchQuery = new JObject { ["bool"] = new JObject { ["must"] = new JArray { secondaryMissing, elasticsearchQuery } } };
                    }
                    else
                    {
                        isHitFromViewDrilldown = true;
                        string secondaryCategoryQuery = string.IsNullOrEmpty(secondaryViewDto.CategoryQuery) ? $"{secondaryViewDto.Aggregation}:\"{secondaryCategory}\"" : secondaryViewDto.CategoryQuery.Replace("{}", secondaryCategory);
                        elasticsearchQuery = new JObject
                        {
                            ["bool"] = new JObject
                            {
                                ["must"] = new JArray
                                {
                                    new JObject { ["query_string"] = new JObject { ["query"] = secondaryCategoryQuery } },
                                    elasticsearchQuery
                                }
                            }
                        };
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
            var spec2 = new SearchSpec<JObject>
            {
                Size = pageSize,
                From = from,
                RawQuery = elasticsearchQuery,
                IncludeDetails = includeDetails,
                PitId = string.IsNullOrWhiteSpace(pitId) ? null : pitId,
                SearchAfter = searchAfter?.Cast<object>().ToList(),
                Sorts = sortDescriptor
            };

            var response = await _entityService.SearchAsync(entity, spec2);
            var objects = response.Hits.Select(h => h.Source).ToList();
            List<object>? searchAfterResponse = null;
            if (response.Hits.Count > 0)
            {
                var lastSorts = response.Hits.Last().Sorts.Where(s => s != null).ToList();
                searchAfterResponse = lastSorts.Count > 0 ? lastSorts : null;
            }
            var hitType = isHitFromViewDrilldown ? "hit" : entity;
            return Ok(new SearchResultDto<JObject>
            {
                Hits = objects,
                TotalHits = response.Total,
                HitType = hitType,
                PitId = response.PointInTimeId,
                searchAfter = searchAfterResponse
            });
        }

        [HttpGet("search/{entity}/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        public async Task<IActionResult> SearchWithLuceneQuery([FromRoute] string entity, [FromQuery] string query,
            [FromQuery] string? view = null, [FromQuery] string? category = null, [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false, [FromQuery] int from = 0, [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null, [FromQuery] string? pitId = null, [FromQuery] string[]? searchAfter = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Query cannot be empty");
            JObject queryObject = new JObject { ["query_string"] = new JObject { ["query"] = query } };
            return await Search(entity, queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpGet("{entity}/unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<IActionResult> GetUniqueFieldValues([FromRoute] string entity, [FromRoute] string field)
        {
            var values = await _entityService.GetUniqueFieldValuesAsync(entity, field + ".keyword");
            return Ok(values);
        }

        [HttpPost("categorize/{entity}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Categorize([FromRoute] string entity, [FromBody] CategorizeRequestDto request)
        {
            if (request.Ids == null || !request.Ids.Any()) return BadRequest("At least one ID must be provided");
            if (string.IsNullOrWhiteSpace(request.Category)) return BadRequest("Category cannot be empty");
            var result = await _entityService.CategorizeAsync(entity, request);
            return Ok(result);
        }

        [HttpPost("categorize-multiple/{entity}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CategorizeMultiple([FromRoute] string entity, [FromBody] CategorizeMultipleRequestDto request)
        {
            if (request.Rows == null || !request.Rows.Any()) return BadRequest("At least one row ID must be provided");
            var result = await _entityService.CategorizeMultipleAsync(entity, request);
            return Ok(result);
        }

        [HttpPost("bql-to-ruleset/{entity}")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public async Task<ActionResult<RulesetDto>> ConvertBqlToRuleset([FromRoute] string entity, [FromBody] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Query cannot be empty");
            var ruleset = await new BqlService<object>(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BqlService<object>>(), _namedQueryService, BqlService<object>.LoadSpec(entity), entity).Bql2Ruleset(query.Trim());
            return Ok(ruleset);
        }

        [HttpPut("{entity}/{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Update([FromRoute] string entity, [FromRoute] string id, [FromBody] JObject document)
        {
            var wr = await _entityService.UpdateAsync(entity, id, document);
            return Ok(new SimpleApiResponse { Success = wr.Success, Message = wr.Message });
        }

        [HttpDelete("{entity}/{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Delete([FromRoute] string entity, [FromRoute] string id)
        {
            var wr = await _entityService.DeleteAsync(entity, id);
            return Ok(new SimpleApiResponse { Success = wr.Success, Message = wr.Message });
        }

        [HttpPost("ruleset-to-bql/{entity}")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<string>> ConvertRulesetToBql([FromRoute] string entity, [FromBody] RulesetDto ruleset)
        {
            var bqlQuery = await new BqlService<object>(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BqlService<object>>(), _namedQueryService, BqlService<object>.LoadSpec(entity), entity).Ruleset2Bql(ruleset);
            return Ok(bqlQuery);
        }

        [HttpPost("ruleset-to-elasticsearch/{entity}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromRoute] string entity, [FromBody] RulesetDto rulesetDto)
        {
            var ruleset = new Ruleset { field = rulesetDto.field, @operator = rulesetDto.@operator, value = rulesetDto.value, condition = rulesetDto.condition, not = rulesetDto.not, rules = rulesetDto.rules?.Select(r => new Ruleset { field = r.field, @operator = r.@operator, value = r.value, condition = r.condition, not = r.not, rules = new List<Ruleset>() }).ToList() ?? new List<Ruleset>() };
            var elasticQuery = await _entityService.ConvertRulesetToElasticSearch(entity, ruleset);
            return Ok(elasticQuery);
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

        // All raw searches and aggregations are handled by EntityService now.
        // Controller contains only orchestration and parameter handling.

        private Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(string entity, JObject queryObject, ViewDto viewDto, int size = 20, int from = 0)
            => _entityService.SearchWithElasticQueryAndViewAsync(entity, queryObject, viewDto, size, from);

        private Task<SimpleApiResponse> CategorizeAsync(string entity, CategorizeRequestDto request)
            => _entityService.CategorizeAsync(entity, request);

        private Task<SimpleApiResponse> CategorizeMultipleAsync(string entity, CategorizeMultipleRequestDto request)
            => _entityService.CategorizeMultipleAsync(entity, request);

        [HttpGet("{entity}/query-builder-spec")]
        [Produces("application/json")]
        public IActionResult GetQueryBuilderSpec([FromRoute] string entity)
        {
            if (_specRegistry.TryGetObject(entity, "queryBuilder", out var qb))
            {
                return Content(qb.ToJsonString(), "application/json");
            }
            return NotFound("Query builder spec not found for entity.");
        }

        [HttpGet("{entity}/spec")]
        [Produces("application/json")]
        public IActionResult GetEntitySpec([FromRoute] string entity)
        {
            var fileName = ($"{entity}" ?? string.Empty).ToLowerInvariant() + ".json";
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Entities", fileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound("Entity spec file not found");
            }
            var json = System.IO.File.ReadAllText(path);
            return Content(json, "application/json");
        }
    }
}
