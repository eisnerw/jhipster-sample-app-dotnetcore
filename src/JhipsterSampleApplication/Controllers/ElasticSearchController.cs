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

        public class BirthdayDto
        {
            public long Id { get; set; }
            public string? ElasticId { get; set; }
            public string? Lname { get; set; }
            public string? Fname { get; set; }
            public string? Sign { get; set; }
            public DateTime? Dob { get; set; }
            public bool? IsAlive { get; set; }
            public string? Text { get; set; }
        }

        public class BirthdayCreateUpdateDto
        {
            public string? ElasticId { get; set; }
            public string? Lname { get; set; }
            public string? Fname { get; set; }
            public string? Sign { get; set; }
            public DateTime? Dob { get; set; }
            public bool? IsAlive { get; set; }
            public string? Text { get; set; }
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
                    Id = b.Id,
                    ElasticId = b.ElasticId,
                    Lname = b.Lname,
                    Fname = b.Fname,
                    Sign = b.Sign,
                    Dob = b.Dob,
                    IsAlive = b.IsAlive,
                    Text = b.Text
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
                Id = b.Id,
                ElasticId = b.ElasticId,
                Lname = b.Lname,
                Fname = b.Fname,
                Sign = b.Sign,
                Dob = b.Dob,
                IsAlive = b.IsAlive,
                Text = b.Text
            };

            return Ok(dto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(IndexResponse), 200)]
        public async Task<IActionResult> Index([FromBody] BirthdayCreateUpdateDto dto)
        {
            var birthday = new Birthday
            {
                ElasticId = dto.ElasticId,
                Lname = dto.Lname,
                Fname = dto.Fname,
                Sign = dto.Sign,
                Dob = dto.Dob,
                IsAlive = dto.IsAlive,
                Text = dto.Text
            };

            var response = await _elasticSearchService.IndexAsync(birthday);
            return Ok(response);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(DeleteResponse), 200)]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _elasticSearchService.DeleteAsync(id);
            return Ok(response);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(UpdateResponse<Birthday>), 200)]
        public async Task<IActionResult> Update(string id, [FromBody] BirthdayCreateUpdateDto dto)
        {
            var birthday = new Birthday
            {
                ElasticId = dto.ElasticId,
                Lname = dto.Lname,
                Fname = dto.Fname,
                Sign = dto.Sign,
                Dob = dto.Dob,
                IsAlive = dto.IsAlive,
                Text = dto.Text
            };

            var response = await _elasticSearchService.UpdateAsync(id, birthday);
            return Ok(response);
        }

        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<IActionResult> GetUniqueFieldValues(string field)
        {
            var values = await _elasticSearchService.GetUniqueFieldValuesAsync(field);
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
    }
}
