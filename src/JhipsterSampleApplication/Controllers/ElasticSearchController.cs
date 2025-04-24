using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nest;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using System.Collections.Generic;
using JHipsterNet.Core.Pagination;
using Microsoft.AspNetCore.Authorization;

namespace JhipsterSampleApplication.Controllers
{
    // Temporarily removing [Authorize] for testing
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

        /// <summary>
        /// Test endpoint - no authentication required
        /// </summary>
        [HttpGet("test")]
        [AllowAnonymous]
        public async Task<IActionResult> Test()
        {
            var response = await _elasticClient.Cluster.HealthAsync();
            return Ok(new { 
                status = response.Status,
                numberOfNodes = response.NumberOfNodes,
                numberOfDataNodes = response.NumberOfDataNodes,
                activeShards = response.ActiveShards,
                activePrimaryShards = response.ActivePrimaryShards
            });
        }

        /// <summary>
        /// Search birthdays using a ruleset
        /// </summary>
        [HttpPost("search")]
        [ProducesResponseType(typeof(ISearchResponse<Birthday>), 200)]
        public async Task<IActionResult> Search([FromBody] RulesetOrRule ruleset, [FromQuery] int size = 10000)
        {
            var response = await _elasticSearchService.SearchWithRulesetAsync(ruleset, size);
            return Ok(response);
        }

        /// <summary>
        /// Get a birthday by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Birthday), 200)]
        public async Task<IActionResult> GetById(string id)
        {
            var response = await _elasticClient.GetAsync<Birthday>(id);
            if (!response.IsValid)
            {
                return NotFound();
            }
            return Ok(response.Source);
        }

        /// <summary>
        /// Create or update a birthday
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(IndexResponse), 200)]
        public async Task<IActionResult> Index([FromBody] Birthday birthday)
        {
            var response = await _elasticSearchService.IndexAsync(birthday);
            return Ok(response);
        }

        /// <summary>
        /// Delete a birthday by ID
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(DeleteResponse), 200)]
        public async Task<IActionResult> Delete(string id)
        {
            var response = await _elasticSearchService.DeleteAsync(id);
            return Ok(response);
        }

        /// <summary>
        /// Update a birthday
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(UpdateResponse<Birthday>), 200)]
        public async Task<IActionResult> Update(string id, [FromBody] Birthday birthday)
        {
            var response = await _elasticSearchService.UpdateAsync(id, birthday);
            return Ok(response);
        }

        /// <summary>
        /// Get unique values for a field
        /// </summary>
        [HttpGet("unique-values/{field}")]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<IActionResult> GetUniqueFieldValues(string field)
        {
            var values = await _elasticSearchService.GetUniqueFieldValuesAsync(field);
            return Ok(values);
        }

        /// <summary>
        /// Get cluster health
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ClusterHealthResponse), 200)]
        public async Task<IActionResult> GetHealth()
        {
            var response = await _elasticClient.Cluster.HealthAsync();
            return Ok(response);
        }
    }
} 