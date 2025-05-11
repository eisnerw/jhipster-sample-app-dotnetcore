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

        public BirthdaysController(
            IBirthdayService birthdayService,
            IElasticClient elasticClient,
            IBirthdayBqlService bqlService,
            ILogger<BirthdaysController> log,
            IMapper mapper)
        {
            _birthdayService = birthdayService;
            _elasticClient = elasticClient;
            _bqlService = bqlService;
            _log = log;
            _mapper = mapper;
        }

        public class SearchResult<T>
        {
            public List<T> Hits { get; set; } = new();
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

        /// <summary>
        /// Search birthdays using a Lucene query
        /// </summary>
        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        public async Task<IActionResult> SearchWithLuceneQuery(
            [FromQuery] string query,
            [FromQuery] int from = 0,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query cannot be empty");
            }

            try
            {
                var searchRequest = new SearchRequest<Birthday>
                {
                    From = from,
                    Size = pageSize,
                    Query = new QueryContainerDescriptor<Birthday>().QueryString(qs => qs.Query(query))
                };

                if (!string.IsNullOrEmpty(sort))
                {
                    var sortParts = sort.Split(':');
                    if (sortParts.Length == 2)
                    {
                        var field = sortParts[0];
                        var order = sortParts[1].ToLower() == "desc" ? SortOrder.Descending : SortOrder.Ascending;
                        searchRequest.Sort = new List<ISort>
                        {
                            new FieldSort { Field = field, Order = order }
                        };
                    }
                }

                var response = await _birthdayService.SearchAsync(searchRequest);

                if (!response.IsValid)
                {
                    _log.LogError($"Invalid search response: {response.DebugInformation}");
                    return BadRequest("Invalid search response");
                }

                var birthdayDtos = response.Hits.Select(hit => new BirthdayDto
                {
                    Id = hit.Id,
                    Text = hit.Source.Text,
                    Lname = hit.Source.Lname,
                    Fname = hit.Source.Fname,
                    Sign = hit.Source.Sign,
                    Dob = hit.Source.Dob,
                    IsAlive = hit.Source.IsAlive,
                    Wikipedia = hit.Source.Wikipedia,
                    Categories = hit.Source.Categories
                }).ToList();

                var result = new SearchResult<BirthdayDto>
                {
                    Hits = birthdayDtos
                };

                Response.Headers["X-Total-Count"] = response.Total.ToString();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error searching birthdays with Lucene query");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Search birthdays using a ruleset
        /// </summary>
        [HttpPost("search/ruleset")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Search([FromBody] RulesetOrRuleDto rulesetDto, 
            [FromQuery] int pageSize = 20,
            [FromQuery] int from = 0,
            [FromQuery] string? sort = null)
        {
            var ruleset = _mapper.Map<RulesetOrRule>(rulesetDto);
            
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

            var response = await _birthdayService.SearchWithRulesetAsync(ruleset, pageSize, from, sortDescriptor);

            var birthdayDtos = response.Hits.Select(hit => new BirthdayDto
            {
                Id = hit.Id,
                Text = hit.Source.Text,
                Lname = hit.Source.Lname,
                Fname = hit.Source.Fname,
                Sign = hit.Source.Sign,
                Dob = hit.Source.Dob,
                IsAlive = hit.Source.IsAlive ?? false,
                Wikipedia = hit.Source.Wikipedia,
                Categories = hit.Source.Categories
            }).ToList();

            return Ok(new SearchResult<BirthdayDto> { Hits = birthdayDtos });
        }

        /// <summary>
        /// Search birthdays using a raw Elasticsearch query
        /// </summary>
        [HttpPost("search/elasticsearch")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Search([FromBody] object elasticsearchQuery,
            [FromQuery] int pageSize = 20,
            [FromQuery] int from = 0,
            [FromQuery] string? sort = null)
        {
            var searchRequest = new SearchRequest<Birthday>
            {
                Size = pageSize,
                From = from
            };
            
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
            var response = await _birthdayService.SearchAsync(searchRequest);

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
                    Wikipedia = b.Wikipedia,
                    Categories = b.Categories
                });
            }

            return Ok(new SearchResult<BirthdayDto> { Hits = birthdayDtos });
        }

        /// <summary>
        /// Search birthdays using a BQL query with pagination
        /// </summary>
        [HttpPost("search/bql")]
        [Consumes("text/plain")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SearchWithBqlPlainText(
            [FromBody] string bqlQuery,
            [FromQuery] int pageSize = 20,
            [FromQuery] int from = 0,
            [FromQuery] string? sort = null)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery))
                return BadRequest("Query cannot be empty");
            try
            {
                var rulesetDto = await _bqlService.Bql2Ruleset(bqlQuery.Trim());
                var ruleset = _mapper.Map<RulesetOrRule>(rulesetDto);

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

                var response = await _birthdayService.SearchWithRulesetAsync(ruleset, pageSize, from, sortDescriptor);
                var birthdayDtos = response.Hits
                    .Select(hit => new BirthdayDto
                    {
                        Id = hit.Id,
                        Text = hit.Source.Text,
                        Lname = hit.Source.Lname,
                        Fname = hit.Source.Fname,
                        Sign = hit.Source.Sign,
                        Dob = hit.Source.Dob,
                        IsAlive = hit.Source.IsAlive ?? false,
                        Wikipedia = hit.Source.Wikipedia,
                        Categories = hit.Source.Categories
                    }).ToList();
                return Ok(new SearchResult<BirthdayDto> { Hits = birthdayDtos });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error searching birthdays with BQL query");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BirthdayDto), 200)]
        public async Task<IActionResult> GetById(string id)
        {
            var searchRequest = new SearchRequest<Birthday>
            {
                Query = new QueryContainerDescriptor<Birthday>().Term(t => t.Field("_id").Value(id))
            };
            
            var response = await _birthdayService.SearchAsync(searchRequest);
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
                Wikipedia = birthday.Wikipedia,
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
            
            var existingResponse = await _birthdayService.SearchAsync(searchRequest);
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
        [ProducesResponseType(typeof(RulesetOrRuleDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public async Task<ActionResult<RulesetOrRuleDto>> ConvertBqlToRuleset([FromBody] string query)
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
        public async Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetOrRuleDto ruleset)
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
        public async Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetOrRuleDto rulesetDto)
        {
            _log.LogDebug("REST request to convert Ruleset to Elasticsearch query");
        
            try
            {
                var ruleset = _mapper.Map<RulesetOrRule>(rulesetDto);
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
    }
}
