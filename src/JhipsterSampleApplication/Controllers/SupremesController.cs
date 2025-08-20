#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nest;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using JhipsterSampleApplication.Dto;
using AutoMapper;
using System.Text;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Controllers
{
	[ApiController]
	[Route("api/supreme")]
	public class SupremesController : ControllerBase
	{
		private readonly ISupremeService _supremeService;
		private readonly IElasticClient _elasticClient;
		private readonly ISupremeBqlService _bqlService;
		private readonly IMapper _mapper;
		private readonly IViewService _viewService;
		private readonly ILogger<SupremesController> _logger;

		public SupremesController(
			ISupremeService supremeService,
			IElasticClient elasticClient,
			ISupremeBqlService bqlService,
			IMapper mapper,
			IViewService viewService,
			ILogger<SupremesController> logger)
		{
			_supremeService = supremeService;
			_elasticClient = elasticClient;
			_bqlService = bqlService;
			_mapper = mapper;
			_viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
			_logger = logger;
		}

                public class RawSearchRequestDto
                {
                        public string? Query { get; set; }
                        public int? From { get; set; }
                        public int? Size { get; set; }
                        public string? Sort { get; set; }
                }

                /// <summary>
                /// Returns an HTML page constructed from various fields for a given Supreme document
                /// </summary>
                [HttpGet("html/{id}")]
                [Produces("text/html")]
                public async Task<IActionResult> GetHtmlById(string id)
                {
                        var searchRequest = new SearchRequest<Supreme>
                        {
                                Query = new QueryContainerDescriptor<Supreme>().Term(t => t.Field("_id").Value(id))
                        };

                        var response = await _supremeService.SearchAsync(searchRequest);
                        if (!response.IsValid || !response.Documents.Any())
                        {
                                return NotFound();
                        }

                        var s = response.Documents.First();
                        string? Join(IEnumerable<string>? list)
                        {
                                if (list == null) return null;
                                var vals = list.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => WebUtility.HtmlEncode(v.Trim())).ToList();
                                return vals.Count > 0 ? string.Join(", ", vals) : null;
                        }
                        string? JoinDissent(string? dissent)
                        {
                                if (string.IsNullOrWhiteSpace(dissent)) return null;
                                var parts = dissent.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
                                return Join(parts);
                        }
                        var sb = new StringBuilder();
                        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><base target=\"_blank\"><title>")
                          .Append(WebUtility.HtmlEncode(s.Name ?? "Supreme"))
                          .Append("</title><style>body{margin:0;padding:8px;font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,Helvetica Neue,Arial,\"Apple Color Emoji\",\"Segoe UI Emoji\";font-size:14px;line-height:1.4;color:#111} .empty{color:#666} .field-name{font-weight:600} .field{margin-bottom:0.7em}</style></head><body>");

                        if (!string.IsNullOrWhiteSpace(s.Name) || !string.IsNullOrWhiteSpace(s.Docket_Number))
                        {
                                sb.Append("<h3>").Append(WebUtility.HtmlEncode(s.Name ?? string.Empty));
                                if (!string.IsNullOrWhiteSpace(s.Docket_Number))
                                {
                                        sb.Append(" (").Append(WebUtility.HtmlEncode(s.Docket_Number)).Append(")");
                                }
                                sb.Append("</h3>");
                        }

                        string FormatLabel(string label)
                        {
                                return string.Join(" ", label.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w =>
                                        (w.Equals("of", StringComparison.OrdinalIgnoreCase) || w.Equals("the", StringComparison.OrdinalIgnoreCase))
                                                ? w.ToLowerInvariant()
                                                : w.Equals("url", StringComparison.OrdinalIgnoreCase)
                                                        ? "URL"
                                                        : char.ToUpperInvariant(w[0]) + w.Substring(1)));
                        }

                        void AppendField(string label, string? value)
                        {
                                if (string.IsNullOrWhiteSpace(value)) return;
                                sb.Append("<div class=\"field\"><span class=\"field-name\">")
                                  .Append(WebUtility.HtmlEncode(FormatLabel(label)))
                                  .Append(":</span> ")
                                  .Append(value)
                                  .Append("</div>");
                        }

                        AppendField("lower court", WebUtility.HtmlEncode(s.Lower_Court ?? string.Empty));
                        AppendField("jurisdiction", WebUtility.HtmlEncode(s.Manner_Of_Jurisdiction ?? string.Empty));
                        AppendField("decision", WebUtility.HtmlEncode(s.Decision ?? string.Empty));
                        AppendField("advocates", Join(s.Advocates));
                        AppendField("description", WebUtility.HtmlEncode(s.Description ?? string.Empty));
                        AppendField("question", WebUtility.HtmlEncode(s.Question ?? string.Empty));
                        AppendField("facts of the case", WebUtility.HtmlEncode(s.Facts_Of_The_Case ?? string.Empty));
                        AppendField("conclusion", WebUtility.HtmlEncode(s.Conclusion ?? string.Empty));
                        AppendField("opinion", WebUtility.HtmlEncode(s.Opinion ?? string.Empty));
                        AppendField("dissent", JoinDissent(s.Dissent));
                        AppendField("justia url", string.IsNullOrWhiteSpace(s.Justia_Url) ? null : "<a href=\"" + WebUtility.HtmlEncode(s.Justia_Url) + "\">" + WebUtility.HtmlEncode(s.Justia_Url) + "</a>");
                        AppendField("argument2 url", string.IsNullOrWhiteSpace(s.Argument2_Url) ? null : "<a href=\"" + WebUtility.HtmlEncode(s.Argument2_Url) + "\">" + WebUtility.HtmlEncode(s.Argument2_Url) + "</a>");
                        AppendField("majority", Join(s.Majority));
                        AppendField("minority", Join(s.Minority));
                        AppendField("recused", Join(s.Recused));

                        sb.Append("</body></html>");
                        return Content(sb.ToString(), "text/html");
                }

		[HttpGet("query-builder-spec")]
		[Produces("application/json")]
		public IActionResult GetQueryBuilderSpec()
		{
			var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "query-builder", "supreme-qb-spec.json");
			if (!System.IO.File.Exists(path))
			{
				return NotFound("Spec file not found");
			}
			var json = System.IO.File.ReadAllText(path);
			return Content(json, "application/json");
		}

		[HttpGet("search/lucene")]
		[ProducesResponseType(typeof(SearchResultDto<object>), 200)]
		public async Task<IActionResult> SearchWithLuceneQuery(
			[FromQuery] string query,
			[FromQuery] int from = 0,
			[FromQuery] int pageSize = 20,
			[FromQuery] string? sort = null,
			[FromQuery] string? pitId = null,
			[FromQuery] string[]? searchAfter = null,
			[FromQuery] string? view = null,
			[FromQuery] string? category = null,
			[FromQuery] string? secondaryCategory = null,
			[FromQuery] bool includeDescriptive = false)
		{
			_logger.LogInformation("Supreme lucene search called. query='{Query}', from={From}, pageSize={PageSize}, sort='{Sort}', view='{View}', category='{Category}', secondaryCategory='{SecondaryCategory}', includeDescriptive={IncludeDescriptive}", query, from, pageSize, sort, view, category, secondaryCategory, includeDescriptive);
			if (string.IsNullOrWhiteSpace(query))
			{
				return BadRequest("Query cannot be empty");
			}
			JObject queryStringObject = new JObject(new JProperty("query", query));
			JObject queryObject = new JObject(new JProperty("query_string", queryStringObject));
			try
			{
				return await Search(queryObject, pageSize, from, sort, view, category, secondaryCategory, pitId, searchAfter, includeDescriptive);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Supreme lucene search failed");
				return StatusCode(500, ex.Message);
			}
		}

		[HttpPost("search/ruleset")]
		[ProducesResponseType(typeof(SearchResultDto<object>), 200)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> SearchRuleset([FromBody] RulesetDto rulesetDto,
			[FromQuery] int pageSize = 20,
			[FromQuery] int from = 0,
			[FromQuery] string? sort = null,
			[FromQuery] string? pitId = null,
			[FromQuery] string[]? searchAfter = null,
			[FromQuery] string? view = null,
			[FromQuery] string? category = null,
			[FromQuery] string? secondaryCategory = null,
			[FromQuery] bool includeDescriptive = false)
		{
			var ruleset = _mapper.Map<Ruleset>(rulesetDto);
			var queryObject = await _supremeService.ConvertRulesetToElasticSearch(ruleset);
			return await Search(queryObject, pageSize, from, sort, view, category, secondaryCategory, pitId, searchAfter, includeDescriptive);
		}

		[HttpPost("search/elasticsearch")]
		[ProducesResponseType(typeof(SearchResultDto<object>), 200)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> Search([FromBody] JObject elasticsearchQuery,
			[FromQuery] int pageSize = 20,
			[FromQuery] int from = 0,
			[FromQuery] string? sort = null,
			[FromQuery] string? view = null,
			[FromQuery] string? category = null,
			[FromQuery] string? secondaryCategory = null,
			[FromQuery] string? pitId = null,
			[FromQuery] string[]? searchAfter = null,
			[FromQuery] bool includeDescriptive = false)
		{
			if (!string.IsNullOrEmpty(view))
			{
				var viewDto = await _viewService.GetByIdAsync(view);
				if (viewDto == null)
				{
					throw new ArgumentException($"view '{view}' not found");
				}
				if (category == null)
				{
					if (secondaryCategory != null)
					{
						throw new ArgumentException($"secondaryCategory '{secondaryCategory}' should be null because category is null");
					}
					var viewResult = await _supremeService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, viewDto, from, pageSize);
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
								new JProperty(viewDto.Aggregation ?? string.Empty, "")
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
					string categoryQuery = string.IsNullOrEmpty(viewDto.CategoryQuery) ?  $"{viewDto.Aggregation}:\"{category}\"" : viewDto.CategoryQuery.Replace("{}", category);
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
						var viewSecondaryResult = await _supremeService.SearchWithElasticQueryAndViewAsync(elasticsearchQuery, secondaryViewDto, from, pageSize);
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
							new JProperty(secondaryViewDto.Aggregation ?? string.Empty, "")
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
						string secondaryCategoryQuery = string.IsNullOrEmpty(secondaryViewDto.CategoryQuery) ?  $"{secondaryViewDto.Aggregation}:\"{secondaryCategory}\"" : secondaryViewDto.CategoryQuery.Replace("{}", secondaryCategory);
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

                        var searchRequest = new SearchRequest<Supreme>
                        {
                                Size = pageSize,
                                From = from,
                                Source = includeDescriptive
                                        ? null
                                        : new SourceFilter
                                        {
                                                Excludes = new[] { "justia_url", "facts_of_the_case", "question", "conclusion" }
                                        }
                        };

                        if (searchAfter != null && searchAfter.Length > 0)
                        {
                                searchRequest.SearchAfter = searchAfter.Cast<object>().ToList();
                                searchRequest.From = null;
                        }

                        var sortDescriptor = new List<ISort>();
			if (!string.IsNullOrEmpty(sort))
			{
				var sortParts = sort.Contains(':') ? sort.Split(':') : (sort.Contains(',') ? sort.Split(',') : Array.Empty<string>());
				if (sortParts.Length == 2)
				{
					var field = sortParts[0];
					var order = sortParts[1].ToLower() == "desc" ? SortOrder.Descending : SortOrder.Ascending;
					if (field == "docket_number")
					{
						_logger.LogInformation("Using docket_number script sort. order={Order}", order);
						var script =
							@"def dn = params._source.containsKey('docket_number') ? params._source.docket_number : null;" +
							@"if (dn == null) return 0L;" +
							@"dn = dn.toString().trim();" +
							@"int idx = dn.indexOf('-');" +
							@"long a = 0; long b = 0;" +
							@"String s0 = idx >= 0 ? dn.substring(0, idx) : dn;" +
							@"String s1 = idx >= 0 ? dn.substring(idx + 1) : """";" +
							@"String d0 = """";" +
							@"for (int i = 0; i < s0.length(); ++i) { def ch = s0.charAt(i); if (ch >= '0' && ch <= '9') { d0 = d0 + s0.substring(i, i+1); } }" +
							@"String d1 = """";" +
							@"for (int i = 0; i < s1.length(); ++i) { def ch = s1.charAt(i); if (ch >= '0' && ch <= '9') { d1 = d1 + s1.substring(i, i+1); } }" +
							@"if (d0.length() > 0) { a = Long.parseLong(d0); }" +
							@"if (d1.length() > 0) { b = Long.parseLong(d1); }" +
							@"return a * 1000000L + b;";
						sortDescriptor.Add(new ScriptSort
						{
							Script = new InlineScript(script),
							Type = "number",
							Order = order
						});
					}
					else
					{
						if (field.Equals("name", StringComparison.OrdinalIgnoreCase))
						{
							_logger.LogInformation("Skipping sort on text field 'name'; falling back to _id only");
						}
						else
						{
							_logger.LogInformation("Using field sort. field={Field}, order={Order}", field, order);
							sortDescriptor.Add(new FieldSort { Field = field, Order = order });
						}
					}
				}
			}
			else
			{
				// Default: rely on _id only (added below) to avoid mapping issues
				_logger.LogInformation("Using default sort by _id only");
			}
			// Always add _id as the last sort field for consistent pagination
			sortDescriptor.Add(new FieldSort { Field = "_id", Order = SortOrder.Ascending });

			searchRequest.Query = new QueryContainerDescriptor<Supreme>().Raw(elasticsearchQuery.ToString());
			searchRequest.Sort = sortDescriptor;
			var response = await _supremeService.SearchAsync(searchRequest, pitId);

			var supremeDtos = new List<object>();
			foreach (var hit in response.Hits)
			{
				var s = hit.Source;
				supremeDtos.Add(new {
					// maintain explicit snake_case keys to match frontend expectations
					id = s.Id!,
					name = s.Name,
					term = s.Term,
					docket_number = s.Docket_Number,
					justia_url = s.Justia_Url,
					decision = s.Decision,
					description = s.Description,
					dissent = s.Dissent,
					lower_court = s.Lower_Court,
					manner_of_jurisdiction = s.Manner_Of_Jurisdiction,
					opinion = s.Opinion,
					argument2_url = s.Argument2_Url,
					appellant = s.Appellant,
					appellee = s.Appellee,
					petitioner = s.Petitioner,
					respondent = s.Respondent,
					recused = s.Recused,
					majority = s.Majority,
					minority = s.Minority,
                                        advocates = s.Advocates,
                                        categories = s.Categories,
                                        facts_of_the_case = s.Facts_Of_The_Case,
					question = s.Question,
					conclusion = s.Conclusion
				});
			}
			List<object>? searchAfterResponse = response.Hits.Count > 0 ? response.Hits.Last().Sorts.ToList() : null;
			return Ok(new SearchResultDto<object> { Hits = supremeDtos.Cast<object>().ToList(), TotalHits = response.Total, HitType = "supreme", PitId = searchRequest.PointInTime?.Id, searchAfter = searchAfterResponse });
		}

		[HttpPost("search/bql")]
		[Consumes("text/plain")]
		[ProducesResponseType(typeof(SearchResultDto<object>), 200)]
		[ProducesResponseType(400)]
                public async Task<IActionResult> SearchWithBqlPlainText(
                        [FromBody] string bqlQuery,
                        [FromQuery] int pageSize = 20,
                        [FromQuery] int from = 0,
			[FromQuery] string? sort = null,
			[FromQuery] string? pitId = null,
			[FromQuery] string[]? searchAfter = null,
			[FromQuery] string? view = null,
			[FromQuery] string? category = null,
			[FromQuery] string? secondaryCategory = null)
		{
			if (string.IsNullOrWhiteSpace(bqlQuery))
			{
				return BadRequest("Query cannot be empty");
			}
                        var rulesetDto = await _bqlService.Bql2Ruleset(bqlQuery.Trim());
                        var ruleset = _mapper.Map<Ruleset>(rulesetDto);
                        var queryObject = await _supremeService.ConvertRulesetToElasticSearch(ruleset);
                        return await Search(queryObject, pageSize, from, sort, view, category, secondaryCategory, pitId, searchAfter);
                }


                [HttpGet("{id}")]
                [ProducesResponseType(typeof(SupremeDto), 200)]
                public async Task<IActionResult> GetById(string id, [FromQuery] bool includeDescriptive = false)
                {
                        var searchRequest = new SearchRequest<Supreme>
                        {
                                Query = new QueryContainerDescriptor<Supreme>().Term(t => t.Field("_id").Value(id)),
                                Source = includeDescriptive ? null : new SourceFilter
                                {
                                        Excludes = new[] { "justia_url", "facts_of_the_case", "question", "conclusion" }
                                }
                        };

                        var response = await _supremeService.SearchAsync(searchRequest, "");
                        if (!response.IsValid || !response.Documents.Any())
                        {
                                return NotFound();
                        }

                        var s = response.Documents.First();
                        var dto = new SupremeDto
                        {
                                Id = id,
                                Name = s.Name,
                                Docket_Number = s.Docket_Number,
                                Manner_Of_Jurisdiction = s.Manner_Of_Jurisdiction,
                                Lower_Court = s.Lower_Court,
                                Facts_Of_The_Case = includeDescriptive ? s.Facts_Of_The_Case : null,
                                Question = includeDescriptive ? s.Question : null,
                                Conclusion = includeDescriptive ? s.Conclusion : null,
                                Decision = s.Decision,
                                Description = s.Description,
                                Dissent = s.Dissent,
                                Heard_By = s.Heard_By,
                                Term = s.Term,
                                Justia_Url = includeDescriptive ? s.Justia_Url : null,
                                Opinion = s.Opinion,
                                Argument2_Url = s.Argument2_Url,
                                Appellant = s.Appellant,
                                Appellee = s.Appellee,
                                Petitioner = s.Petitioner,
                                Respondent = s.Respondent,
                                Recused = s.Recused,
                                Majority = s.Majority,
                                Minority = s.Minority,
                                Advocates = s.Advocates,
                                Categories = s.Categories ?? new List<string>()
                        };

                        return Ok(dto);
                }

                [HttpPost]
                [ProducesResponseType(typeof(SimpleApiResponse), 200)]
                public async Task<IActionResult> Create([FromBody] SupremeCreateUpdateDto dto)
                {
                        var supreme = new Supreme
                        {
                                Id = dto.Id,
                                Name = dto.Name,
                                Docket_Number = dto.Docket_Number,
                                Manner_Of_Jurisdiction = dto.Manner_Of_Jurisdiction,
                                Lower_Court = dto.Lower_Court,
                                Facts_Of_The_Case = dto.Facts_Of_The_Case,
                                Question = dto.Question,
                                Conclusion = dto.Conclusion,
                                Decision = dto.Decision,
                                Description = dto.Description,
                                Dissent = dto.Dissent,
                                Heard_By = dto.Heard_By,
                                Term = dto.Term,
                                Justia_Url = dto.Justia_Url,
                                Opinion = dto.Opinion,
                                Argument2_Url = dto.Argument2_Url,
                                Appellant = dto.Appellant,
                                Appellee = dto.Appellee,
                                Petitioner = dto.Petitioner,
                                Respondent = dto.Respondent,
                                Recused = dto.Recused,
                                Majority = dto.Majority,
                                Minority = dto.Minority,
                                Advocates = dto.Advocates,
                                Categories = dto.Categories ?? new List<string>()
                        };

                        var response = await _supremeService.IndexAsync(supreme);
                        return Ok(new SimpleApiResponse { Success = response.IsValid, Message = response.DebugInformation.Split('\n')[0] });
                }

                [HttpDelete("{id}")]
                [ProducesResponseType(typeof(SimpleApiResponse), 200)]
                public async Task<IActionResult> Delete(string id)
                {
                        var response = await _supremeService.DeleteAsync(id);
                        return Ok(new SimpleApiResponse { Success = response.IsValid, Message = response.DebugInformation.Split('\n')[0] });
                }

                [HttpPut("{id}")]
                [ProducesResponseType(typeof(SimpleApiResponse), 200)]
                public async Task<IActionResult> Update(string id, [FromBody] SupremeCreateUpdateDto dto)
                {
                        var searchRequest = new SearchRequest<Supreme>
                        {
                                Query = new QueryContainerDescriptor<Supreme>().Term(t => t.Field("_id").Value(id))
                        };

                        var existing = await _supremeService.SearchAsync(searchRequest, "");
                        if (!existing.IsValid || !existing.Documents.Any())
                        {
                                return NotFound($"Document with ID {id} not found");
                        }

                        var supreme = new Supreme
                        {
                                Id = id,
                                Name = dto.Name,
                                Docket_Number = dto.Docket_Number,
                                Manner_Of_Jurisdiction = dto.Manner_Of_Jurisdiction,
                                Lower_Court = dto.Lower_Court,
                                Facts_Of_The_Case = dto.Facts_Of_The_Case,
                                Question = dto.Question,
                                Conclusion = dto.Conclusion,
                                Decision = dto.Decision,
                                Description = dto.Description,
                                Dissent = dto.Dissent,
                                Heard_By = dto.Heard_By,
                                Term = dto.Term,
                                Justia_Url = dto.Justia_Url,
                                Opinion = dto.Opinion,
                                Argument2_Url = dto.Argument2_Url,
                                Appellant = dto.Appellant,
                                Appellee = dto.Appellee,
                                Petitioner = dto.Petitioner,
                                Respondent = dto.Respondent,
                                Recused = dto.Recused,
                                Majority = dto.Majority,
                                Minority = dto.Minority,
                                Advocates = dto.Advocates,
                                Categories = dto.Categories ?? new List<string>()
                        };

                        var updateResponse = await _supremeService.UpdateAsync(id, supreme);
                        return Ok(new SimpleApiResponse { Success = updateResponse.IsValid, Message = updateResponse.DebugInformation.Split('\n')[0] });
                }

                [HttpGet("unique-values/{field}")]
                [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
                public async Task<IActionResult> GetUniqueFieldValues(string field)
                {
                        var esField = field == "term" ? field : field + ".keyword";
                        var values = await _supremeService.GetUniqueFieldValuesAsync(esField);
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
                                return BadRequest(ex.Message);
                        }
                }

                [HttpPost("ruleset-to-bql")]
                [ProducesResponseType(typeof(string), 200)]
                [ProducesResponseType(400)]
                public async Task<ActionResult<string>> ConvertRulesetToBql([FromBody] RulesetDto ruleset)
                {
                        try
                        {
                                var bqlQuery = await _bqlService.Ruleset2Bql(ruleset);
                                return Ok(bqlQuery);
                        }
                        catch (ArgumentException ex)
                        {
                                return BadRequest(ex.Message);
                        }
                        catch (Exception)
                        {
                                return StatusCode(500, "An error occurred while converting Ruleset to BQL");
                        }
                }

                [HttpPost("ruleset-to-bql-to-ruleset")]
                [ProducesResponseType(typeof(RulesetDto), 200)]
                [ProducesResponseType(400)]
                public async Task<ActionResult<RulesetDto>> ConvertRulesetToBqlToRuleset([FromBody] RulesetDto ruleset)
                {
                        try
                        {
                                var bqlQuery = await _bqlService.Ruleset2Bql(ruleset);
                                var roundTrip = await _bqlService.Bql2Ruleset(bqlQuery);
                                return Ok(roundTrip);
                        }
                        catch (ArgumentException ex)
                        {
                                return BadRequest(ex.Message);
                        }
                        catch (Exception)
                        {
                                return StatusCode(500, "An error occurred while converting Ruleset");
                        }
                }

                [HttpPost("ruleset-to-elasticsearch")]
                [ProducesResponseType(typeof(object), 200)]
                [ProducesResponseType(400)]
                public async Task<ActionResult<object>> ConvertRulesetToElasticSearch([FromBody] RulesetDto rulesetDto)
                {
                        try
                        {
                                var ruleset = _mapper.Map<Ruleset>(rulesetDto);
                                var elasticQuery = await _supremeService.ConvertRulesetToElasticSearch(ruleset);
                                return Ok(elasticQuery);
                        }
                        catch (ArgumentException ex)
                        {
                                return BadRequest(ex.Message);
                        }
                        catch (Exception)
                        {
                                return StatusCode(500, "An error occurred while converting Ruleset to Elasticsearch query");
                        }
                }

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

                        var searchRequest = new SearchRequest<Supreme>
                        {
                                Query = new QueryContainerDescriptor<Supreme>().Terms(t => t.Field("_id").Terms(request.Ids))
                        };

                        var response = await _supremeService.SearchAsync(searchRequest, "");
                        if (!response.IsValid)
                        {
                                return BadRequest("Failed to search for supremes");
                        }

                        var successCount = 0;
                        var errorCount = 0;
                        var errorMessages = new List<string>();

                        foreach (var supreme in response.Documents)
                        {
                                try
                                {
                                        if (request.RemoveCategory)
                                        {
                                                if (supreme.Categories != null)
                                                {
                                                        var toRemove = supreme.Categories.FirstOrDefault(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase));
                                                        if (toRemove != null)
                                                        {
                                                                supreme.Categories.Remove(toRemove);
                                                                var updateResponse = await _supremeService.UpdateAsync(supreme.Id!, supreme);
                                                                if (updateResponse.IsValid)
                                                                {
                                                                        successCount++;
                                                                }
                                                                else
                                                                {
                                                                        errorCount++;
                                                                        errorMessages.Add($"Failed to update supreme {supreme.Id}: {updateResponse.DebugInformation}");
                                                                }
                                                        }
                                                }
                                        }
                                        else
                                        {
                                                if (supreme.Categories == null)
                                                {
                                                        supreme.Categories = new List<string>();
                                                }
                                                if (!supreme.Categories.Any(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase)))
                                                {
                                                        supreme.Categories.Add(request.Category);
                                                        var updateResponse = await _supremeService.UpdateAsync(supreme.Id!, supreme);
                                                        if (updateResponse.IsValid)
                                                        {
                                                                successCount++;
                                                        }
                                                        else
                                                        {
                                                                errorCount++;
                                                                errorMessages.Add($"Failed to update supreme {supreme.Id}: {updateResponse.DebugInformation}");
                                                        }
                                                }
                                        }
                                }
                                catch (Exception ex)
                                {
                                        errorCount++;
                                        errorMessages.Add($"Error processing supreme {supreme.Id}: {ex.Message}");
                                }
                        }

                        var message = $"Processed {request.Ids.Count} supremes. Success: {successCount}, Errors: {errorCount}";
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

                [HttpPost("categorize-multiple")]
                [ProducesResponseType(typeof(SimpleApiResponse), 200)]
                [ProducesResponseType(400)]
                public async Task<IActionResult> CategorizeMultiple([FromBody] CategorizeMultipleRequestDto request)
                {
                        if (request.Rows == null || !request.Rows.Any())
                        {
                                return BadRequest("At least one row ID must be provided");
                        }

                        var toAdd = (request.Add ?? new List<string>())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                        var toRemove = (request.Remove ?? new List<string>())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim())
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                        if (!toAdd.Any() && !toRemove.Any())
                        {
                                return BadRequest("Nothing to add or remove");
                        }

                        var searchRequest = new SearchRequest<Supreme>
                        {
                                Query = new QueryContainerDescriptor<Supreme>().Terms(t => t.Field("_id").Terms(request.Rows))
                        };

                        var response = await _supremeService.SearchAsync(searchRequest, "");
                        if (!response.IsValid)
                        {
                                return BadRequest("Failed to search for supremes");
                        }

                        var successCount = 0;
                        var errorCount = 0;
                        var errorMessages = new List<string>();

                        foreach (var supreme in response.Documents)
                        {
                                try
                                {
                                        var current = supreme.Categories ?? new List<string>();

                                        if (toRemove.Any() && current.Any())
                                        {
                                                current = current.Where(c => !toRemove.Any(r => string.Equals(c, r, StringComparison.OrdinalIgnoreCase))).ToList();
                                        }

                                        foreach (var add in toAdd)
                                        {
                                                if (!current.Any(c => string.Equals(c, add, StringComparison.OrdinalIgnoreCase)))
                                                {
                                                        current.Add(add);
                                                }
                                        }

                                        supreme.Categories = current;
                                        var updateResponse = await _supremeService.UpdateAsync(supreme.Id!, supreme);
                                        if (updateResponse.IsValid)
                                        {
                                                successCount++;
                                        }
                                        else
                                        {
                                                errorCount++;
                                                errorMessages.Add($"Failed to update supreme {supreme.Id}: {updateResponse.DebugInformation}");
                                        }
                                }
                                catch (Exception ex)
                                {
                                        errorCount++;
                                        errorMessages.Add($"Error processing supreme {supreme.Id}: {ex.Message}");
                                }
                        }

                        var message = $"Processed {request.Rows.Count} supremes. Success: {successCount}, Errors: {errorCount}";
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