#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
            if (!_specRegistry.TryGet(entity, out var spec)) throw new ArgumentException($"Unknown entity '{entity}'", nameof(entity));
            var idField = string.IsNullOrWhiteSpace(spec.IdField) ? "Id" : spec.IdField!;
            return (spec.Index, spec.DescriptiveFields ?? Array.Empty<string>(), idField);
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
            var queryObject = await ConvertRulesetToElasticSearch(entity, ruleset);
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
            var queryObject = await ConvertRulesetToElasticSearch(entity, ruleset);
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
            var elasticQuery = await ConvertRulesetToElasticSearch(entity, ruleset);
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

        private async Task<AppSearchResponse<JObject>> SearchAsync(string entity, SearchSpec<JObject> spec)
        {
            var (index, details, _) = GetEntityConfig(entity);
            return await SearchRawAsync(index, details, spec);
        }

        private async Task<AppSearchResponse<JObject>> SearchRawAsync(string index, string[] detailFields, SearchSpec<JObject> spec)
        {
            var url = _configuration["Elasticsearch:Url"]?.TrimEnd('/') ?? "http://localhost:9200";
            var username = _configuration["Elasticsearch:Username"];
            var password = _configuration["Elasticsearch:Password"];

            string? pitId = spec.PitId;
            if (string.IsNullOrWhiteSpace(pitId))
            {
                try
                {
                    var pitResponse = await _elasticClient.OpenPointInTimeAsync(new Elastic.Clients.Elasticsearch.OpenPointInTimeRequest(index) { KeepAlive = "2m" });
                    if (pitResponse.IsValidResponse) pitId = pitResponse.Id;
                }
                catch { }
            }

            var usePit = !string.IsNullOrEmpty(pitId);
            var path = usePit ? "/_search" : $"/{index}/_search";

            var root = new JObject();
            var hasSearchAfter = spec.SearchAfter != null && spec.SearchAfter.Count > 0;
            if (spec.From.HasValue && !hasSearchAfter) root["from"] = spec.From.Value;
            if (spec.Size.HasValue) root["size"] = spec.Size.Value;
            if (!spec.IncludeDetails && detailFields.Length > 0)
            {
                root["_source"] = new JObject { ["excludes"] = new JArray(detailFields) };
            }
            var sorts = new JArray();
            if (spec.Sorts != null && spec.Sorts.Count > 0)
            {
                foreach (var s in spec.Sorts)
                {
                    if (!string.IsNullOrWhiteSpace(s.Script)) continue;
                    sorts.Add(new JObject { [s.Field] = new JObject { ["order"] = (s.Order?.ToLower() == "desc" ? "desc" : "asc") } });
                }
            }
            else if (!string.IsNullOrWhiteSpace(spec.Sort))
            {
                var parts = spec.Sort.Contains(',') ? spec.Sort.Split(',') : spec.Sort.Split(':');
                if (parts.Length == 2)
                {
                    sorts.Add(new JObject { [parts[0].Trim()] = new JObject { ["order"] = (parts[1].Trim().ToLower() == "desc" ? "desc" : "asc") } });
                }
            }
            sorts.Add(new JObject { ["_id"] = new JObject { ["order"] = "asc" } });
            root["sort"] = sorts;

            if (hasSearchAfter)
            {
                var sa = new JArray();
                foreach (var o in spec.SearchAfter!) sa.Add(JToken.FromObject(o));
                root["search_after"] = sa;
                root.Remove("from");
            }
            if (usePit)
            {
                root["pit"] = new JObject { ["id"] = pitId, ["keep_alive"] = "2m" };
            }
            root["query"] = spec.RawQuery ?? new JObject { ["match_all"] = new JObject() };

            using var http = new HttpClient();
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes($"{username}:{password}");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
            }
            var req = new HttpRequestMessage(HttpMethod.Post, url + path)
            {
                Content = new StringContent(root.ToString(), System.Text.Encoding.UTF8, "application/json")
            };
            var resp = await http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"ES search failed: {(int)resp.StatusCode} {json}");

            var parsed = JObject.Parse(json);
            var hitsToken = parsed["hits"]?["hits"] as JArray ?? new JArray();
            var total = parsed["hits"]?["total"]?[(object)"value"]?.Value<long?>() ?? parsed["hits"]?["total"]?.Value<long?>() ?? hitsToken.Count;
            var app = new AppSearchResponse<JObject> { Total = total, PointInTimeId = pitId, IsValid = true };
            foreach (var h in hitsToken)
            {
                var id = h["_id"]?.ToString() ?? string.Empty;
                var src = h["_source"] as JObject ?? new JObject();
                if (src["Id"] == null) src["Id"] = id;
                var sortsToken = h["sort"] as JArray;
                var sortsList = new List<object>();
                if (sortsToken != null)
                {
                    foreach (var x in sortsToken)
                    {
                        object val;
                        if (x.Type == JTokenType.Integer) val = (long)x;
                        else if (x.Type == JTokenType.Float) val = (double)x;
                        else val = x.Value<string>() ?? x.ToString();
                        sortsList.Add(val);
                    }
                }
                app.Hits.Add(new AppHit<JObject> { Id = id, Source = src, Sorts = sortsList });
            }
            return app;
        }

        private async Task<List<string>> GetUniqueFieldValuesAsync(string entity, string field)
        {
            var (index, _, _) = GetEntityConfig(entity);
            var body = new JObject
            {
                ["size"] = 0,
                ["aggs"] = new JObject
                {
                    ["distinct"] = new JObject
                    {
                        ["terms"] = new JObject
                        {
                            ["field"] = field,
                            ["size"] = 10000
                        }
                    }
                }
            };
            var json = await PostRawAsync(index, body, null, true);
            var arr = (json["aggregations"]?["distinct"]?["buckets"] as JArray) ?? new JArray();
            var ret = new List<string>();
            foreach (var b in arr)
            {
                var v = b["key"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(v)) ret.Add(v);
            }
            return ret;
        }

        private async Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(string entity, JObject queryObject, ViewDto viewDto, int size = 20, int from = 0)
        {
            var (index, _, _) = GetEntityConfig(entity);
            var root = new JObject
            {
                ["size"] = 0,
                ["query"] = queryObject,
                ["aggs"] = new JObject
                {
                    ["distinct"] = string.IsNullOrEmpty(viewDto.Script)
                        ? new JObject { ["terms"] = new JObject { ["field"] = viewDto.Aggregation, ["size"] = 10000 } }
                        : new JObject { ["terms"] = new JObject { ["script"] = new JObject { ["source"] = viewDto.Script }, ["size"] = 10000 } }
                }
            };
            var distinct = await PostRawAsync(index, root, null, true);
            var buckets = (distinct["aggregations"]?["distinct"]?["buckets"] as JArray) ?? new JArray();
            var content = new List<ViewResultDto>();
            foreach (var b in buckets)
            {
                string categoryName = b["key"]?.ToString() ?? b["key_as_string"]?.ToString() ?? string.Empty;
                bool notCategorized = string.IsNullOrEmpty(categoryName);
                if (notCategorized) categoryName = "(Uncategorized)";
                content.Add(new ViewResultDto { CategoryName = categoryName, Count = b["doc_count"]?.Value<long?>(), NotCategorized = notCategorized });
            }
            content = content.OrderBy(cat => cat.CategoryName).ToList();
            return content;
        }

        private async Task<SimpleApiResponse> CategorizeAsync(string entity, CategorizeRequestDto request)
        {
            var spec = new SearchSpec<JObject> { Ids = request.Ids, IncludeDetails = true, PitId = "" };
            var response = await SearchAsync(entity, spec);
            if (!response.IsValid) return new SimpleApiResponse { Success = false, Message = "Failed to search for the entities" };
            var successCount = 0; var errorCount = 0; var errorMessages = new List<string>();
            foreach (var hit in response.Hits)
            {
                var obj = hit.Source;
                var id = hit.Id;
                try
                {
                    var categories = (obj["Categories"] as JArray)?.ToObject<List<string>>() ?? new List<string>();
                    if (request.RemoveCategory)
                    {
                        var remove = categories.FirstOrDefault(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase));
                        if (remove != null) { categories = categories.Where(c => !string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase)).ToList(); }
                    }
                    else
                    {
                        if (!categories.Any(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase))) categories.Add(request.Category);
                    }
                    obj["Categories"] = JArray.FromObject(categories);
                    var updateResp = await UpdateAsync(entity, id, obj);
                    if (updateResp.Success) successCount++; else { errorCount++; errorMessages.Add($"Failed to update entity {id}: {updateResp.Message}"); }
                }
                catch (Exception ex) { errorCount++; errorMessages.Add($"Error processing entity {id}: {ex.Message}"); }
            }
            await _elasticClient.Indices.RefreshAsync(GetEntityConfig(entity).Index);
            var message = $"Processed {request.Ids.Count} entities. Success: {successCount}, Errors: {errorCount}";
            if (errorMessages.Any()) message += $". Error details: {string.Join("; ", errorMessages)}";
            return new SimpleApiResponse { Success = errorCount == 0, Message = message };
        }

        private async Task<SimpleApiResponse> CategorizeMultipleAsync(string entity, CategorizeMultipleRequestDto request)
        {
            var toAdd = (request.Add ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var toRemove = (request.Remove ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!toAdd.Any() && !toRemove.Any()) return new SimpleApiResponse { Success = false, Message = "Nothing to add or remove" };
            var spec = new SearchSpec<JObject> { Ids = request.Rows, IncludeDetails = true, PitId = "" };
            var response = await SearchAsync(entity, spec);
            if (!response.IsValid) return new SimpleApiResponse { Success = false, Message = "Failed to search for entities" };
            var successCount = 0; var errorCount = 0; var errorMessages = new List<string>();
            foreach (var hit in response.Hits)
            {
                var obj = hit.Source; var id = hit.Id;
                try
                {
                    var current = (obj["Categories"] as JArray)?.ToObject<List<string>>() ?? new List<string>();
                    if (toRemove.Any() && current.Any()) current = current.Where(c => !toRemove.Any(r => string.Equals(c, r, StringComparison.OrdinalIgnoreCase))).ToList();
                    foreach (var add in toAdd) if (!current.Any(c => string.Equals(c, add, StringComparison.OrdinalIgnoreCase))) current.Add(add);
                    obj["Categories"] = JArray.FromObject(current);
                    var updateResp = await UpdateAsync(entity, id, obj);
                    if (updateResp.Success) successCount++; else { errorCount++; errorMessages.Add($"Failed to update entity {id}: {updateResp.Message}"); }
                }
                catch (Exception ex) { errorCount++; errorMessages.Add($"Error processing entity {id}: {ex.Message}"); }
            }
            await _elasticClient.Indices.RefreshAsync(GetEntityConfig(entity).Index);
            var message = $"Processed {request.Rows.Count} entities. Success: {successCount}, Errors: {errorCount}";
            if (errorMessages.Any()) message += $". Error details: {string.Join("; ", errorMessages)}";
            return new SimpleApiResponse { Success = errorCount == 0, Message = message };
        }

        private async Task<WriteResult> UpdateAsync(string entity, string id, JObject document)
        {
            var (index, _, _) = GetEntityConfig(entity);
            var resp = await _elasticClient.IndexAsync<object>(document.ToObject<object>()!, i => i.Index(index).Id(id).Refresh(Elastic.Clients.Elasticsearch.Refresh.WaitFor));
            return new WriteResult { Success = resp.IsValidResponse, Message = resp.Result.ToString() };
        }

        private async Task<JObject> ConvertRulesetToElasticSearch(string entity, Ruleset ruleset)
        {
            var bql = new BqlService<object>(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BqlService<object>>(), _namedQueryService, BqlService<object>.LoadSpec(entity), entity);
            var dto = new RulesetDto { field = ruleset.field, @operator = ruleset.@operator, value = ruleset.value, condition = ruleset.condition, not = ruleset.not, rules = new List<RulesetDto>() };
            var result = await bql.Ruleset2ElasticSearch(dto);
            return result is JObject jo ? jo : JObject.FromObject(result);
        }

        private async Task<JObject> PostRawAsync(string index, JObject body, string? pitId, bool useIndexPath)
        {
            var url = _configuration["Elasticsearch:Url"]?.TrimEnd('/') ?? "http://localhost:9200";
            var username = _configuration["Elasticsearch:Username"];
            var password = _configuration["Elasticsearch:Password"];
            var path = useIndexPath ? $"/{index}/_search" : "/_search";
            if (!string.IsNullOrEmpty(pitId)) body["pit"] = new JObject { ["id"] = pitId, ["keep_alive"] = "2m" };
            using var http = new HttpClient();
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes($"{username}:{password}");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
            }
            var req = new HttpRequestMessage(HttpMethod.Post, url + path) { Content = new StringContent(body.ToString(), System.Text.Encoding.UTF8, "application/json") };
            var resp = await http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception($"ES raw post failed: {(int)resp.StatusCode} {json}");
            return JObject.Parse(json);
        }
    }
}
