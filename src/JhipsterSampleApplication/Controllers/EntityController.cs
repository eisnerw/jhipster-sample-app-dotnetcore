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
using System.Text.Json.Nodes;
using System.Text;
using System.Net;

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/entity")]
    public class EntityController : ControllerBase
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly IConfiguration _configuration;
        private readonly IEntitySpecRegistry _specRegistry;
        private readonly INamedQueryService _namedQueryService;
        private readonly JhipsterSampleApplication.Domain.Services.EntityService _entityService;

        public EntityController(ElasticsearchClient elasticClient, IConfiguration configuration, IEntitySpecRegistry specRegistry, INamedQueryService namedQueryService, JhipsterSampleApplication.Domain.Services.EntityService jsonService)
        {
            _elasticClient = elasticClient;
            _configuration = configuration;
            _specRegistry = specRegistry;
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

        [HttpGet("{entity}/html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById([FromRoute] string entity, [FromRoute] string id)
        {
            var spec = new SearchSpec<JObject> { Id = id, IncludeDetails = true };
            var response = await _entityService.SearchAsync(entity, spec);
            if (!response.IsValid || !response.Documents.Any()) return NotFound();
            var entityValues = response.Documents.First();
            if (!_specRegistry.TryGetArray(entity, "detailHtmlTemplate", out var arr))
            {
                return Ok(Enumerable.Empty<ViewDto>());
            }
            string[] arTemplate = arr.Select(n => n!.GetValue<string>()).ToArray();
            string html = JhipsterSampleApplication.Domain.Services.EntityService.InterpolateTemplate(arTemplate, entityValues);
            return Content(html, "text/html");            
        }

        [HttpPost("{entity}/search/bql")]
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
            // Prefer spec from entity registry; fall back to legacy file if needed
            Newtonsoft.Json.Linq.JObject qbSpec;
            if (_specRegistry.TryGetObject(entity, "queryBuilder", out JsonObject qbNode))
            {
                qbSpec = Newtonsoft.Json.Linq.JObject.Parse(qbNode.ToJsonString());
            }
            else
            {
                var entitiesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Entities", $"{entity}.json");
                if (System.IO.File.Exists(entitiesPath))
                {
                    var root = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(entitiesPath));
                    qbSpec = (root["queryBuilder"] as Newtonsoft.Json.Linq.JObject) ?? new Newtonsoft.Json.Linq.JObject();
                }
                else
                {
                    qbSpec = BqlService<object>.LoadSpec(entity);
                }
            }
            var rulesetDto = await new BqlService<object>(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BqlService<object>>(), _namedQueryService, qbSpec, entity).Bql2Ruleset(bqlQuery.Trim());
            var ruleset = MapRuleset(rulesetDto);
            var queryObject = await _entityService.ConvertRulesetToElasticSearch(entity, ruleset);
            return await Search(entity, queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("{entity}/search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<JObject>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithRuleset([FromRoute] string entity, [FromBody] RulesetDto rulesetDto,
            [FromQuery] string? view = null, [FromQuery] string? category = null, [FromQuery] string? secondaryCategory = null,
            [FromQuery] bool includeDetails = false, [FromQuery] int from = 0, [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null, [FromQuery] string? pitId = null, [FromQuery] string[]? searchAfter = null)
        {
            var ruleset = MapRuleset(rulesetDto);
            var queryObject = await _entityService.ConvertRulesetToElasticSearch(entity, ruleset);
            return await Search(entity, queryObject, view, category, secondaryCategory, includeDetails, from, pageSize, sort, pitId, searchAfter);
        }

        [HttpPost("{entity}/search/elasticsearch")]
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
                var viewDto = GetViewById(entity, view);
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
                var secondaryViewDto = GetChildViewByParentId(entity, view);
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

        [HttpGet("{entity}/search/lucene")]
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

        [HttpPost("{entity}/categorize")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Categorize([FromRoute] string entity, [FromBody] CategorizeRequestDto request)
        {
            if (request.Ids == null || !request.Ids.Any()) return BadRequest("At least one ID must be provided");
            if (string.IsNullOrWhiteSpace(request.Category)) return BadRequest("Category cannot be empty");
            var result = await _entityService.CategorizeAsync(entity, request);
            return Ok(result);
        }

        [HttpPost("{entity}/categorize-multiple")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CategorizeMultiple([FromRoute] string entity, [FromBody] CategorizeMultipleRequestDto request)
        {
            if (request.Rows == null || !request.Rows.Any()) return BadRequest("At least one row ID must be provided");
            var result = await _entityService.CategorizeMultipleAsync(entity, request);
            return Ok(result);
        }

        [HttpPost("{entity}/bql-to-ruleset")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public async Task<ActionResult<RulesetDto>> ConvertBqlToRuleset([FromRoute] string entity, [FromBody] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Query cannot be empty");
            Newtonsoft.Json.Linq.JObject qbSpec;
            if (_specRegistry.TryGetObject(entity, "queryBuilder", out JsonObject qbNode))
            {
                qbSpec = Newtonsoft.Json.Linq.JObject.Parse(qbNode.ToJsonString());
            }
            else
            {
                var entitiesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Entities", $"{entity}.json");
                if (System.IO.File.Exists(entitiesPath))
                {
                    var root = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(entitiesPath));
                    qbSpec = (root["queryBuilder"] as Newtonsoft.Json.Linq.JObject) ?? new Newtonsoft.Json.Linq.JObject();
                }
                else
                {
                    qbSpec = BqlService<object>.LoadSpec(entity);
                }
            }
            var ruleset = await new BqlService<object>(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BqlService<object>>(), _namedQueryService, qbSpec, entity).Bql2Ruleset(query.Trim());
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

        [HttpPost("{entity}/ruleset-to-bql")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<string>> ConvertRulesetToBql([FromRoute] string entity, [FromBody] RulesetDto ruleset)
        {
            Newtonsoft.Json.Linq.JObject qbSpec;
            if (_specRegistry.TryGetObject(entity, "queryBuilder", out JsonObject qbNode))
            {
                qbSpec = Newtonsoft.Json.Linq.JObject.Parse(qbNode.ToJsonString());
            }
            else
            {
                var entitiesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Entities", $"{entity}.json");
                if (System.IO.File.Exists(entitiesPath))
                {
                    var root = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(entitiesPath));
                    qbSpec = (root["queryBuilder"] as Newtonsoft.Json.Linq.JObject) ?? new Newtonsoft.Json.Linq.JObject();
                }
                else
                {
                    qbSpec = BqlService<object>.LoadSpec(entity);
                }
            }
            var bqlQuery = await new BqlService<object>(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BqlService<object>>(), _namedQueryService, qbSpec, entity).Ruleset2Bql(ruleset);
            return Ok(bqlQuery);
        }

        [HttpPost("{entity}/ruleset-to-elasticsearch")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromRoute] string entity, [FromBody] RulesetDto rulesetDto)
        {
            var ruleset = MapRuleset(rulesetDto);
            var elasticQuery = await _entityService.ConvertRulesetToElasticSearch(entity, ruleset);
            return Ok(elasticQuery);
        }

        private static Ruleset MapRuleset(RulesetDto dto)
        {
            if (dto == null) return new Ruleset();
            return new Ruleset
            {
                field = dto.field,
                @operator = dto.@operator,
                value = dto.value,
                condition = dto.condition,
                @not = dto.not,
                rules = dto.rules?.Select(MapRuleset).ToList()
            };
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

        private ViewDto? GetViewById(string entity, string idOrName)
        {
            if (_specRegistry.TryGetArray(entity, "views", out var arr))
            {
                foreach (var node in arr.OfType<JsonObject>())
                {
                    var id = node["id"]?.GetValue<string>();
                    var name = node["name"]?.GetValue<string>();
                    if (string.Equals(id, idOrName, StringComparison.OrdinalIgnoreCase) || string.Equals(name, idOrName, StringComparison.OrdinalIgnoreCase))
                    {
                        return MapView(node);
                    }
                }
            }
            return null;
        }

        private ViewDto? GetChildViewByParentId(string entity, string parentId)
        {
            if (_specRegistry.TryGetArray(entity, "views", out var arr))
            {
                var matches = arr.OfType<JsonObject>()
                    .Where(v => string.Equals(v["parentViewId"]?.GetValue<string>(), parentId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 1) return MapView(matches[0]);
            }
            return null;
        }

        private static ViewDto MapView(JsonObject o)
        {
            return new ViewDto
            {
                Id = o["id"]?.GetValue<string>(),
                Name = o["name"]?.GetValue<string>(),
                Field = o["field"]?.GetValue<string>(),
                Aggregation = o["aggregation"]?.GetValue<string>(),
                Query = o["query"]?.GetValue<string>(),
                CategoryQuery = o["categoryQuery"]?.GetValue<string>(),
                Script = o["script"]?.GetValue<string>(),
                parentViewId = o["parentViewId"]?.GetValue<string>(),
                Entity = o["entity"]?.GetValue<string>()
            };
        }

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
