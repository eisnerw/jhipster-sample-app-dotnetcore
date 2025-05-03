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

namespace JhipsterSampleApplication.Controllers
{
    [ApiController]
    [Route("api/elasticsearch")]
    public class ElasticSearchController : ControllerBase
    {
        private readonly IElasticSearchService _elasticSearchService;       
        private readonly IElasticClient _elasticClient;

        public ElasticSearchController(
            IElasticSearchService elasticSearchService,
            IElasticClient elasticClient)
        {
            _elasticSearchService = elasticSearchService;
            _elasticClient = elasticClient;
        }

        public class RulesetOrRuleDto
        {
            public string? field { get; set; }
            public string? @operator { get; set; }
            public object? value { get; set; }
            public string? condition { get; set; }
            public bool @not { get; set; }
            public List<object> rules { get; set; } = new List<object>();
        }

        public class BirthdayCreateUpdateDto
        {
            public string? Id { get; set; }
            public string? Lname { get; set; }
            public string? Fname { get; set; }
            public string? Sign { get; set; }
            public DateTime? Dob { get; set; }
            public bool? IsAlive { get; set; }
            public string? Text { get; set; }
            public string? Wikipedia { get; set; }
            public List<long>? CategoryIds { get; set; }  // Flattened relationship
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
            var response = await _elasticSearchService.SearchWithRulesetAsync(ruleset, size);

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

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BirthdayDto), 200)]
        public async Task<IActionResult> GetById(string id)
        {
            var response = await _elasticClient.GetAsync<Birthday>(id);
            if (!response.IsValid || response.Source == null)
            {
                return NotFound();
            }

            var b = response.Source;
            var dto = new BirthdayDto
            {
                Id = response.Id,
                Lname = b.Lname,
                Fname = b.Fname,
                Sign = b.Sign,
                Dob = b.Dob,
                IsAlive = b.IsAlive,
                Text = b.Text,
                Wikipedia = b.Wikipedia
            };

            return Ok(dto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Index([FromBody] BirthdayCreateUpdateDto dto)
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

            var response = await _elasticSearchService.IndexAsync(birthday);
            
            // Return both the response and the ID of the indexed document
            return Ok(new { 
                Success = response.IsValid,
                Message = response.DebugInformation.Split('\n')[0]
            });
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _elasticSearchService.DeleteAsync(id);
            return Ok(new {
                Success = response.IsValid,
                Message = response.DebugInformation.Split('\n')[0]
            });
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Update(string id, [FromBody] BirthdayCreateUpdateDto dto)
        {
            // First, try to get the existing document to preserve its ID
            var existingResponse = await _elasticClient.GetAsync<Birthday>(id);
            if (!existingResponse.IsValid || existingResponse.Source == null)
            {
                return NotFound($"Document with ID {id} not found");
            }
            
            // Update the existing document with the new values
            var birthday = new Birthday
            {
                Id = id, // Use the ID from the URL
                Lname = dto.Lname,
                Fname = dto.Fname,
                Sign = dto.Sign,
                Dob = dto.Dob,
                IsAlive = dto.IsAlive,
                Text = dto.Text,
                Wikipedia = dto.Wikipedia
            };

            UpdateResponse<Birthday> updateResponse = await _elasticSearchService.UpdateAsync(id, birthday);
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
            var values = await _elasticSearchService.GetUniqueFieldValuesAsync(field+".keyword");
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

        [HttpGet("search/lucene")]
        [ProducesResponseType(typeof(SearchResult<BirthdayDto>), 200)]
        public async Task<IActionResult> SearchWithLuceneQuery([FromQuery] string query)
        {
            var response = await _elasticSearchService.SearchWithLuceneQueryAsync(query);

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

            var response = await _elasticSearchService.SearchAsync(searchRequest);

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
    }
}
