#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nest;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using System.Collections.Generic;
using JHipsterNet.Core.Pagination;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.IO;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;
using AutoMapper;
using System.Collections.ObjectModel;

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/birthdays")]
    public class BirthdaysController : ControllerBase
    {
        private readonly IBirthdayService _birthdayService;       
        private readonly IElasticClient _elasticClient;
        private readonly IBirthdayBqlService _bqlService;
        private readonly ILogger<BirthdaysController> _log;
        private readonly IMapper _mapper;
        private readonly IViewService _viewService;

        public BirthdaysController(
            IBirthdayService birthdayService,
            IElasticClient elasticClient,
            IBirthdayBqlService bqlService,
            ILogger<BirthdaysController> log,
            IMapper mapper,
            IViewService viewService)
        {
            _birthdayService = birthdayService;
            _elasticClient = elasticClient;
            _bqlService = bqlService;
            _log = log;
            _mapper = mapper;
            _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
        }

        public class ClusterHealthDto
        {
            public string Status { get; set; } = string.Empty;
            public int NumberOfNodes { get; set; }
            public int NumberOfDataNodes { get; set; }
            public int ActiveShards { get; set; }
            public int ActivePrimaryShards { get; set; }
        }
        public class SimpleApiResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }

        public class RawSearchRequestDto
        {
            public string? Query { get; set; }
            public int? From { get; set; }
            public int? Size { get; set; }
            public string? Sort { get; set; }
        }

        public class BqlQueryDto
        {
            public string Query { get; set; } = string.Empty;
        }

        public class CategorizeRequestDto
        {
            public List<string> Ids { get; set; } = new List<string>();
            public string Category { get; set; } = string.Empty;
            public bool RemoveCategory { get; set; }
        }

        /// <summary>
        /// Search birthdays using a Lucene query
        /// </summary>
        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        public async Task<IActionResult> SearchWithLuceneQuery(
            [FromQuery] string query,
            [FromQuery] int from = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null,
            [FromQuery] bool includeWikipedia = false,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null)
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
            return await Search(queryObject, pageSize, from, sort, includeWikipedia, view, category, secondaryCategory, pitId, searchAfter);
        }

        /// <summary>
        /// Search birthdays using a ruleset
        /// </summary>
        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchRuleset([FromBody] RulesetDto rulesetDto, 
            [FromQuery] int pageSize = 20,
            [FromQuery] int from = 0,
            [FromQuery] string? sort = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null,
            [FromQuery] bool includeWikipedia = false,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null)
        {
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var queryObject = await _birthdayService.ConvertRulesetToElasticSearch(ruleset);
            return await Search(queryObject, pageSize, from, sort, includeWikipedia, view, category, secondaryCategory, pitId, searchAfter);
        }

        /// <summary>
        /// Search birthdays using a raw Elasticsearch query
        /// </summary>
        [HttpPost("search/elasticsearch")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Search([FromBody] JObject elasticsearchQuery,
            [FromQuery] int pageSize = 20,
            [FromQuery] int from = 0,
            [FromQuery] string? sort = null,
            [FromQuery] bool includeWikipedia = false,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null)
        {
           if (!string.IsNullOrEmpty(view))
            {   
                var viewDto = await _viewService.GetByIdAsync(view);
                if (viewDto == null){
                    throw new ArgumentException($"view '{view}' not found");
                }
                if (category == null)
                {
                    if (secondaryCategory != null){
                        throw new ArgumentException($"secondaryCategory '{secondaryCategory}' should be null because category is null");
                    }
                    var viewResult = await _birthdayService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, viewDto, from, pageSize);
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
                                        new JProperty(viewDto.Aggregation, "")
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
                    string categoryQuery = string.IsNullOrEmpty(viewDto.CategoryQuery) ?  $"{viewDto.Aggregation}:\\\"{category}\\\"" : viewDto.CategoryQuery.Replace("{}", category);
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
                        var viewSecondaryResult = await _birthdayService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, secondaryViewDto, from, pageSize);
                        return Ok(new SearchResultDto<ViewResultDto> { Hits = viewSecondaryResult, HitType = "view", ViewName = view, viewCategory = category });
                    }
                    if (secondaryCategory == "(Uncategorized)")
                    {
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
                                            new JProperty(secondaryViewDto.Aggregation, "")
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
                        string secondaryCategoryQuery = string.IsNullOrEmpty(secondaryViewDto.CategoryQuery) ?  $"{secondaryViewDto.Aggregation}:\\\"{secondaryCategory}\\\"" : secondaryViewDto.CategoryQuery.Replace("{}", secondaryCategory);
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
            var searchRequest = new SearchRequest<Birthday>
            {
                Size = pageSize,
                From = from
            };
            
            if (searchAfter != null && searchAfter.Length > 0)
            {
                searchRequest.SearchAfter = searchAfter.Cast<object>().ToList();
                searchRequest.From = null; 
            }

            // Build sort descriptor
            var sortDescriptor = new List<ISort>();
            if (!string.IsNullOrEmpty(sort))
            {
                var sortParts = sort.Split(':');
                if (sortParts.Length == 2)
                {
                    var field = sortParts[0];
                    var order = sortParts[1].ToLower() == "desc" ? SortOrder.Descending : SortOrder.Ascending;
                    sortDescriptor.Add(new FieldSort { Field = field, Order = order });
                }
            }
            // Always add _id as the last sort field for consistent pagination
            sortDescriptor.Add(new FieldSort { Field = "_id", Order = SortOrder.Ascending });
            
            searchRequest.Query = new QueryContainerDescriptor<Birthday>().Raw(elasticsearchQuery.ToString());
            searchRequest.Sort = sortDescriptor;
            var response = await _birthdayService.SearchAsync(searchRequest, pitId);

            var birthdayDtos = new List<BirthdayDto>();
            foreach (var hit in response.Hits)
            {
                var b = hit.Source;
                birthdayDtos.Add(new BirthdayDto
                {
                    Id = b.Id!,
                    Lname = b.Lname,
                    Fname = b.Fname,
                    Sign = b.Sign,
                    Dob = b.Dob,
                    IsAlive = b.IsAlive,
                    Text = b.Text,
                    Wikipedia = includeWikipedia ? b.Wikipedia : null,
                    Categories = b.Categories
                });
            }
            List<object>? searchAfterResponse = response.Hits.Count > 0 ? response.Hits.Last().Sorts.ToList() : null; 
            return Ok(new SearchResultDto<BirthdayDto> { Hits = birthdayDtos, TotalHits = response.Total, HitType = "birthday", PitId = searchRequest.PointInTime.Id, searchAfter = searchAfterResponse });
        }

        /// <summary>
        /// Search birthdays using a BQL query with pagination
        /// </summary>
        [HttpPost("search/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResultDto<BirthdayDto>), 200)]
        [ProducesResponseType(typeof(SearchResultDto<ViewResultDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithBqlPlainText(
            [FromBody] string bqlQuery,
            [FromQuery] int pageSize = 20,
            [FromQuery] int from = 0,
            [FromQuery] string? sort = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null,
            [FromQuery] bool includeWikipedia = false,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery)){
                return BadRequest("Query cannot be empty");
            }
            var rulesetDto = await _bqlService.Bql2Ruleset(bqlQuery.Trim());
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var queryObject = await _birthdayService.ConvertRulesetToElasticSearch(ruleset);
            return await Search(queryObject, pageSize, from, sort, includeWikipedia, view, category, secondaryCategory, pitId, searchAfter);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BirthdayDto), 200)]
        public async Task<IActionResult> GetById(string id, [FromQuery] bool includeWikipedia = false)
        {
            var searchRequest = new SearchRequest<Birthday>
            {
                Query = new QueryContainerDescriptor<Birthday>().Term(t => t.Field("_id").Value(id))
            };
            
            var response = await _birthdayService.SearchAsync(searchRequest, "");
            if (!response.IsValid || !response.Documents.Any())
            {
                return NotFound();
            }

            var birthday = response.Documents.First();
            var birthdayDto = new BirthdayDto
            {
                Id = id,
                Text = birthday.Text,
                Lname = birthday.Lname,
                Fname = birthday.Fname,
                Sign = birthday.Sign,
                Dob = birthday.Dob,
                IsAlive = birthday.IsAlive ?? false,
                Wikipedia = includeWikipedia ? birthday.Wikipedia : null,
                Categories = birthday.Categories
            };

            return Ok(birthdayDto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Create([FromBody] BirthdayCreateUpdateDto dto)
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
            var simpleResponse = new SimpleApiResponse
            {
                Success = response.IsValid,
                Message = response.DebugInformation.Split('\n')[0]
            };
            return Ok(simpleResponse);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _birthdayService.DeleteAsync(id);
            var simpleResponse = new SimpleApiResponse
            {
                Success = response.IsValid,
                Message = response.DebugInformation.Split('\n')[0]
            };
            return Ok(simpleResponse);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Update(string id, [FromBody] BirthdayCreateUpdateDto dto)
        {
            // Check if document exists using the service layer
            var searchRequest = new SearchRequest<Birthday>
            {
                Query = new QueryContainerDescriptor<Birthday>().Term(t => t.Field("_id").Value(id))
            };
            
            var existingResponse = await _birthdayService.SearchAsync(searchRequest, "");
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
            var simpleResponse = new SimpleApiResponse
            {
                Success = updateResponse.IsValid,
                Message = updateResponse.DebugInformation.Split('\n')[0]
            };
            return Ok(simpleResponse);
        }

        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<IActionResult> GetUniqueFieldValues(string field)
        {
            var values = await _birthdayService.GetUniqueFieldValuesAsync(field+".keyword");
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

        /// <summary>
        /// Converts a BQL query string to a Ruleset
        /// </summary>
        /// <returns>The converted Ruleset</returns>
        /// <response code="200">Returns the converted Ruleset</response>
        /// <response code="400">If the query is invalid or empty</response>
        [HttpPost("bql-to-ruleset")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(RulesetDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public async Task<ActionResult<RulesetDto>> ConvertBqlToRuleset([FromBody] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query cannot be empty");
                }

                var ruleset = await _bqlService.Bql2Ruleset(query.Trim());
                return Ok(ruleset);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error converting BQL to ruleset");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Converts a Ruleset to a BQL query string
        /// </summary>
        /// <param name="ruleset">The Ruleset to convert</param>
        /// <returns>The converted BQL query string</returns>
        [HttpPost("ruleset-to-bql")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetDto ruleset)
        {
            _log.LogDebug("REST request to convert Ruleset to BQL");
            try
            {
                var bqlQuery = await _bqlService.Ruleset2Bql(ruleset);
                return Ok(bqlQuery);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error converting Ruleset to BQL");
                return StatusCode(500, "An error occurred while converting Ruleset to BQL");
            }
        }

        /// <summary>
        /// Converts a Ruleset to an Elasticsearch query
        /// </summary>
        /// <param name="ruleset">The Ruleset to convert</param>
        /// <returns>The converted Elasticsearch query</returns>
        [HttpPost("ruleset-to-elasticsearch")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetDto rulesetDto)
        {
            _log.LogDebug("REST request to convert Ruleset to Elasticsearch query");
        
            try
            {
                var ruleset = _mapper.Map<Ruleset>(rulesetDto);
                var elasticQuery = await _birthdayService.ConvertRulesetToElasticSearch(ruleset);
                return Ok(elasticQuery);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error converting Ruleset to Elasticsearch query");
                return StatusCode(500, "An error occurred while converting Ruleset to Elasticsearch query");
            }
        }

        /// <summary>
        /// Add or remove a category from multiple birthdays
        /// </summary>
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

            var searchRequest = new SearchRequest<Birthday>
            {
                Query = new QueryContainerDescriptor<Birthday>().Terms(t => t.Field("_id").Terms(request.Ids))
            };
            
            var response = await _birthdayService.SearchAsync(searchRequest, "");
            if (!response.IsValid)
            {
                return BadRequest("Failed to search for birthdays");
            }

            var successCount = 0;
            var errorCount = 0;
            var errorMessages = new List<string>();

            foreach (var birthday in response.Documents)
            {
                try
                {
                    if (request.RemoveCategory)
                    {
                        // Remove category if it exists (case-insensitive)
                        if (birthday.Categories != null)
                        {
                            var categoryToRemove = birthday.Categories.FirstOrDefault(c => 
                                string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase));
                            if (categoryToRemove != null)
                            {
                                birthday.Categories.Remove(categoryToRemove);
                                var updateResponse = await _birthdayService.UpdateAsync(birthday.Id!, birthday);
                                if (updateResponse.IsValid)
                                {
                                    successCount++;
                                }
                                else
                                {
                                    errorCount++;
                                    errorMessages.Add($"Failed to update birthday {birthday.Id}: {updateResponse.DebugInformation}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Add category if it doesn't exist (case-insensitive)
                        if (birthday.Categories == null)
                        {
                            birthday.Categories = new List<string>();
                        }
                        if (!birthday.Categories.Any(c => 
                            string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase)))
                        {
                            birthday.Categories.Add(request.Category);
                            var updateResponse = await _birthdayService.UpdateAsync(birthday.Id!, birthday);
                            if (updateResponse.IsValid)
                            {
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                                errorMessages.Add($"Failed to update birthday {birthday.Id}: {updateResponse.DebugInformation}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errorMessages.Add($"Error processing birthday {birthday.Id}: {ex.Message}");
                }
            }

            var message = $"Processed {request.Ids.Count} birthdays. Success: {successCount}, Errors: {errorCount}";
            if (errorMessages.Any())
            {
                message += $". Error details: {string.Join("; ", errorMessages)}";
            }

            return Ok(new SimpleApiResponse
            {
                Success = errorCount == 0,
                Message = message
            });
        }
    }
}
