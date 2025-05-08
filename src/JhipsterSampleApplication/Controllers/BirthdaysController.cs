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

        public BirthdaysController(
            IBirthdayService birthdayService,
            IElasticClient elasticClient,
            IBirthdayBqlService bqlService,
            ILogger<BirthdaysController> log)
        {
            _birthdayService = birthdayService;
            _elasticClient = elasticClient;
            _bqlService = bqlService;
            _log = log;
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

        private RulesetOrRule ConvertDtoToModel(object dto)
        {
            var jObj = dto switch
            {
                JObject jobj => jobj,
                JToken jtoken => jtoken as JObject,
                string json => JObject.Parse(json),
                _ => JObject.FromObject(dto)
            };

            if (jObj?.ContainsKey("rules") == true && jObj["rules"] is JArray rulesArray && rulesArray.Count > 0)
            {
                return new RulesetOrRule
                {
                    condition = jObj["condition"]?.ToString(),
                    @not = jObj["not"]?.ToObject<bool>() ?? false,
                    rules = jObj["rules"] is JArray array
                        ? array.Select(rule => ConvertDtoToModel((JObject)rule)).ToList()
                        : new List<RulesetOrRule>()
                };
            }
            return new RulesetOrRule
            {
                field = jObj?["field"]?.ToString(),
                @operator = jObj?["operator"]?.ToString(),
                value = jObj?["value"]?.ToObject<object>()
            };
        }

        [HttpPost("search")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        public async Task<IActionResult> Search([FromBody] RulesetOrRuleDto rulesetDto, [FromQuery] int size = 10000)
        {
            var ruleset = ConvertDtoToModel(rulesetDto);
            var response = await _birthdayService.SearchWithRulesetAsync(ruleset, size);

            var birthdayDtos = response.Hits.Select(hit => new BirthdayDto
            {
                Id = hit.Id,
                Text = hit.Source.Text,
                Lname = hit.Source.Lname,
                Fname = hit.Source.Fname,
                Sign = hit.Source.Sign,
                Dob = hit.Source.Dob,
                IsAlive = hit.Source.IsAlive ?? false,
                Wikipedia = hit.Source.Wikipedia
            }).ToList();

            return Ok(new SearchResult<BirthdayDto> { Hits = birthdayDtos });
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
                Wikipedia = birthday.Wikipedia
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
                Wikipedia = dto.Wikipedia
            };

            var updateResponse = await _birthdayService.UpdateAsync(id, birthday);
            var simpleResponse = new SimpleApiResponse
            {
                Success = updateResponse.IsValid,
                Message = updateResponse.DebugInformation.Split('\n')[0]
            };
            return Ok(simpleResponse);
        }

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        public async Task<IActionResult> SearchWithLuceneQuery([FromQuery] string query, [FromQuery] int? from = 0, [FromQuery] int? size = 20)
        {
            var response = await _birthdayService.SearchWithLuceneQueryAsync(query, from ?? 0, size ?? 20);

            var birthdayDtos = response.Hits.Select(hit => new BirthdayDto
            {
                Id = hit.Id,
                Text = hit.Source.Text,
                Lname = hit.Source.Lname,
                Fname = hit.Source.Fname,
                Sign = hit.Source.Sign,
                Dob = hit.Source.Dob,
                IsAlive = hit.Source.IsAlive,
                Wikipedia = hit.Source.Wikipedia
            }).ToList();

            var result = new SearchResult<BirthdayDto>
            {
                Hits = birthdayDtos
            };
            Response.Headers["X-Total-Count"] = response.Total.ToString();
            return Ok(result);
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

        [HttpPost("search/raw")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        public async Task<IActionResult> Search([FromBody] object elasticsearchQuery)
        {
            // Set default parameters
            var searchRequest = new SearchRequest<Birthday>
            {
                Size = 10000,
                From = 0
            };
            
            // Parse the JSON and apply it to the search request
            try
            {
                string jsonString;
                
                // Handle different input types
                if (elasticsearchQuery is string jsonStr)
                {
                    jsonString = jsonStr;
                }
                else
                {
                    jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(elasticsearchQuery);
                }
                
                // Parse the JSON to validate it
                var jsonObject = JObject.Parse(jsonString);
                
                // Apply the query from the JSON
                var queryToken = jsonObject["query"];
                if (queryToken != null)
                {
                    var queryString = queryToken.ToString();
                    if (!string.IsNullOrEmpty(queryString))
                    {
                        searchRequest.Query = new QueryContainerDescriptor<Birthday>()
                            .Raw(queryString);
                    }
                }
                
                // Apply sorting if present
                var sortToken = jsonObject["sort"];
                if (sortToken != null)
                {
                    var sortString = sortToken.ToString();
                    if (!string.IsNullOrEmpty(sortString))
                    {
                        searchRequest.Sort = new List<ISort>
                        {
                            new FieldSort { Field = sortString.Trim('"', '\'') }
                        };
                    }
                }
                
                // Apply pagination if present (override defaults)
                var fromToken = jsonObject["from"];
                if (fromToken != null && fromToken.Type != JTokenType.Null)
                {
                    var fromValue = fromToken.Value<int?>();
                    if (fromValue.HasValue)
                    {
                        searchRequest.From = fromValue.Value;
                    }
                }
                
                var sizeToken = jsonObject["size"];
                if (sizeToken != null && sizeToken.Type != JTokenType.Null)
                {
                    var sizeValue = sizeToken.Value<int?>();
                    if (sizeValue.HasValue)
                    {
                        searchRequest.Size = sizeValue.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new SimpleApiResponse
                {
                    Success = false,
                    Message = $"Invalid Elasticsearch query: {ex.Message}"
                });
            }

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
                    Wikipedia = b.Wikipedia
                });
            }

            return Ok(new SearchResult<BirthdayDto> { Hits = birthdayDtos });
        }

        /// <summary>
        /// Converts a BQL query string to a Ruleset
        /// </summary>
        /// <param name="query">The BQL query string to convert (e.g. "sign = \"aries\"")</param>
        /// <param name="from">Starting index for pagination (default: 0)</param>
        /// <param name="size">Number of results per page (default: 20)</param>
        /// <returns>The converted Ruleset</returns>
        /// <response code="200">Returns the converted Ruleset</response>
        /// <response code="400">If the query is invalid or empty</response>
        [HttpGet("bql-to-ruleset")]
        [ProducesResponseType(typeof(RulesetOrRuleDto), 200)]
        [ProducesResponseType(400)]
        [Produces("application/json")]
        public async Task<ActionResult<RulesetOrRuleDto>> ConvertBqlToRuleset(
            [FromQuery(Name = "query")] string query,
            [FromQuery(Name = "from")] int? from = 0,
            [FromQuery(Name = "size")] int? size = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { error = "The BQL query cannot be empty." });
            }

            try
            {
                var ruleset = await _bqlService.Bql2Ruleset(query);
                return Ok(ruleset);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error converting BQL to ruleset");
                return BadRequest(new { error = ex.Message });
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
    }
}
