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

		public SupremesController(
			ISupremeService supremeService,
			IElasticClient elasticClient,
			ISupremeBqlService bqlService,
			IMapper mapper,
			IViewService viewService)
		{
			_supremeService = supremeService;
			_elasticClient = elasticClient;
			_bqlService = bqlService;
			_mapper = mapper;
			_viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
		}

		public class RawSearchRequestDto
		{
			public string? Query { get; set; }
			public int? From { get; set; }
			public int? Size { get; set; }
			public string? Sort { get; set; }
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
			[FromQuery] string? secondaryCategory = null)
		{
			if (string.IsNullOrWhiteSpace(query))
			{
				return BadRequest("Query cannot be empty");
			}
			JObject queryStringObject = new JObject(new JProperty("query", query));
			JObject queryObject = new JObject(new JProperty("query_string", queryStringObject));
			return await Search(queryObject, pageSize, from, sort, view, category, secondaryCategory, pitId, searchAfter);
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
			[FromQuery] string? secondaryCategory = null)
		{
			var ruleset = _mapper.Map<Ruleset>(rulesetDto);
			var queryObject = await _supremeService.ConvertRulesetToElasticSearch(ruleset);
			return await Search(queryObject, pageSize, from, sort, view, category, secondaryCategory, pitId, searchAfter);
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
			[FromQuery] string[]? searchAfter = null)
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
				Index = "supreme",
				Size = pageSize,
				From = from
			};

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

			searchRequest.Query = new QueryContainerDescriptor<Supreme>().Raw(elasticsearchQuery.ToString());
			searchRequest.Sort = sortDescriptor;
			var response = await _supremeService.SearchAsync(searchRequest, pitId);

			var supremeDtos = new List<object>();
			foreach (var hit in response.Hits)
			{
				var s = hit.Source;
				supremeDtos.Add(new {
					Id = s.Id!,
					Name = s.Name,
					Term = s.Term,
					Docket_Number = s.Docket_Number,
					Heard_By = s.Heard_By,
					Justia_Url = s.Justia_Url
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
	}
}