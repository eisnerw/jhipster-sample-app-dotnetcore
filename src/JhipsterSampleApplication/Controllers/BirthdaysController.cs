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
        private readonly IHistoryService _historyService;

        public BirthdaysController(
            IBirthdayService birthdayService,
            IElasticClient elasticClient,
            IBirthdayBqlService bqlService,
            ILogger<BirthdaysController> log,
            IMapper mapper,
            IHistoryService historyService)
        {
            _birthdayService = birthdayService;
            _elasticClient = elasticClient;
            _bqlService = bqlService;
            _log = log;
            _mapper = mapper;
            _historyService = historyService;
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
        /// Returns an HTML page constructed from the Wikipedia attribute for a given Birthday document
        /// </summary>
        [HttpGet("html/{id}")]
        [Produces("text/html")]
        public async Task<IActionResult> GetHtmlById(string id)
        {
            var html = await _birthdayService.GetHtmlByIdAsync(id);
            if (html == null)
            {
                return NotFound();
            }
            return Content(html, "text/html");
        }

        /// <summary>
        /// Returns the Query Builder specification for Birthdays
        /// </summary>
        [HttpGet("query-builder-spec")]
        [Produces("application/json")]
        public IActionResult GetQueryBuilderSpec()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "query-builder", "birthday-qb-spec.json");
            if (!System.IO.File.Exists(path))
            {
                return NotFound("Spec file not found");
            }
            var json = System.IO.File.ReadAllText(path);
            return Content(json, "application/json");
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
            [FromQuery] bool includeDetails = false,
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
            var result = await _birthdayService.Search(queryObject, pageSize, from, sort, includeDetails, view, category, secondaryCategory, pitId, searchAfter);
            return Ok(result);
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
            [FromQuery] bool includeDetails = false,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null)
        {
            var ruleset = _mapper.Map<Ruleset>(rulesetDto);
            var queryObject = await _birthdayService.ConvertRulesetToElasticSearch(ruleset);
            var result = await _birthdayService.Search(queryObject, pageSize, from, sort, includeDetails, view, category, secondaryCategory, pitId, searchAfter);
            return Ok(result);
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
            [FromQuery] bool includeDetails = false,
            [FromQuery] string? view = null,
            [FromQuery] string? category = null,
            [FromQuery] string? secondaryCategory = null,
            [FromQuery] string? pitId = null,
            [FromQuery] string[]? searchAfter = null)
        {
            var result = await _birthdayService.Search(elasticsearchQuery, pageSize, from, sort, includeDetails, view, category, secondaryCategory, pitId, searchAfter);
            return Ok(result);
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
            [FromQuery] bool includeDetails = false,
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
            await _historyService.Save(new History { User = User?.Identity?.Name, Domain = "birthday", Text = bqlQuery });
            var result = await _birthdayService.Search(queryObject, pageSize, from, sort, includeDetails, view, category, secondaryCategory, pitId, searchAfter);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BirthdayDto), 200)]
        public async Task<IActionResult> GetById(string id, [FromQuery] bool includeDetails = false)
        {
            var birthdayDto = await _birthdayService.GetByIdAsync(id, includeDetails);
            if (birthdayDto == null)
            {
                return NotFound();
            }
            return Ok(birthdayDto);
        }

        [HttpPost]
        [ProducesResponseType(typeof(SimpleApiResponse), 200)]
        public async Task<IActionResult> Create([FromBody] BirthdayCreateUpdateDto dto)
        {
            // Best-effort de-duplication to keep test/index state deterministic across runs:
            // if both last and first names are provided, remove any existing docs matching that pair.
            if (!string.IsNullOrWhiteSpace(dto.Lname) && !string.IsNullOrWhiteSpace(dto.Fname))
            {
                try
                {
                    var deleteResponse = await _elasticClient.DeleteByQueryAsync<Birthday>(d => d
                        .Index("birthdays")
                        .Query(q => q.Bool(b => b.Must(
                            m => m.Term(t => t.Field("lname.keyword").Value(dto.Lname)),
                            m => m.Term(t => t.Field("fname.keyword").Value(dto.Fname))
                        )))
                    );
                }
                catch { /* ignore cleanup errors */ }
            }
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
            var result = await _birthdayService.CategorizeAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Add and/or remove multiple categories across multiple birthdays
        /// </summary>
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
    }
}
