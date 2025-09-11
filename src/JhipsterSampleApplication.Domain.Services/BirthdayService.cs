using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Nest;
using Elasticsearch.Net;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using JhipsterSampleApplication.Dto;
using System.Threading;
using System.IO;
using System.Text;
using System.Collections.Specialized;
using System.Net;

namespace JhipsterSampleApplication.Domain.Services;

/// <summary>
/// Service for interacting with Elasticsearch for Birthday operations
/// </summary>
public class BirthdayService : EntityService<Birthday>, IBirthdayService
{
    private readonly IElasticClient _elasticClient;
    private readonly IBqlService<Birthday> _bqlService;
    private readonly IViewService _viewService;

    /// <summary>
    /// Initializes a new instance of the BirthdayService
    /// </summary>
    /// <param name="elasticClient">The Elasticsearch client</param>
    /// <param name="bqlService">The BQL service</param>
    /// <param name="viewService">The View service</param>
    public BirthdayService(IElasticClient elasticClient, IBqlService<Birthday> bqlService, IViewService viewService)
    : base("birthdays", "wikipedia", elasticClient, bqlService, viewService)
    {
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
        _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
    }

    private static bool ValidateRuleset(Ruleset rr)
    {
        if (rr.rules == null || rr.rules.Count == 0)
        {
            return !string.IsNullOrWhiteSpace(rr.field);
        }
        return rr.rules.All(ValidateRuleset);
    }

    private static RulesetDto MapToDto(Ruleset rr)
    {
        return new RulesetDto
        {
            field = rr.field,
            @operator = rr.@operator,
            value = rr.value,
            condition = rr.condition,
            @not = rr.@not,
            rules = rr.rules?.Select(MapToDto).ToList() ?? new List<RulesetDto>()
        };
    }

    public async Task<string?> GetHtmlByIdAsync(string id)
    {
        var searchRequest = new SearchRequest<Birthday>
        {
            Query = new QueryContainerDescriptor<Birthday>().Term(t => t.Field("_id").Value(id))
        };
        var response = await SearchAsync(searchRequest, includeDetails: true, "");
        if (!response.IsValid || !response.Documents.Any())
        {
            return null;
        }
        var birthday = response.Documents.First();
        var fullName = ($"{birthday.Fname} {birthday.Lname}").Trim();
        var wikipediaHtml = birthday.Wikipedia ?? string.Empty;
        var title = string.IsNullOrWhiteSpace(fullName) ? "Birthday" : fullName;
        var html = "<!doctype html>" +
                   "<html><head>" +
                   "<meta charset=\"utf-8\">" +
                   "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                   "<base target=\"_blank\">" +
                   "<title>" + WebUtility.HtmlEncode(title) + "</title>" +
                   "<style>body{margin:0;padding:8px;font-family:system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,Noto Sans,Helvetica Neue,Arial,\"Apple Color Emoji\",\"Segoe UI Emoji\";font-size:14px;line-height:1.4;color:#111} .empty{color:#666}</style>" +
                   "</head><body>" +
                   (string.IsNullOrWhiteSpace(wikipediaHtml)
                       ? ("<div class=\"empty\">No Wikipedia content available." + (string.IsNullOrWhiteSpace(fullName) ? string.Empty : (" for " + WebUtility.HtmlEncode(fullName))) + "</div>")
                       : wikipediaHtml)
                   + "</body></html>";
        return html;
    }

    public async Task<object> Search(JObject elasticsearchQuery, int pageSize = 20, int from = 0, string? sort = null,
        bool includeDetails = false, string? view = null, string? category = null, string? secondaryCategory = null,
        string? pitId = null, string[]? searchAfter = null)
    {
        bool isHitFromViewDrilldown = false;
        if (!string.IsNullOrEmpty(view))
        {
            try
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
                try
                {
                    var viewResult = await SearchWithElasticQueryAndViewAsync(elasticsearchQuery, viewDto, pageSize, from);
                    return new SearchResultDto<ViewResultDto> { Hits = viewResult, HitType = "view", ViewName = view };
                }
                catch
                {
                    // Fallback: compute only the (Uncategorized) bucket defensively to avoid 500
                    var baseField = (viewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
                    var missingQuery = new JObject(
                        new JProperty("bool", new JObject(
                            new JProperty("must", new JArray(
                                elasticsearchQuery,
                                new JObject(
                                    new JProperty("bool", new JObject(
                                        new JProperty("must_not", new JArray(
                                            new JObject(new JProperty("exists", new JObject(new JProperty("field", baseField))))
                                        ))
                                    ))
                                )
                            ))
                        ))
                    );
                    var missingSearch = new SearchRequest<Birthday>
                    {
                        Size = 0,
                        Query = new QueryContainerDescriptor<Birthday>().Raw(missingQuery.ToString())
                    };
                    var missResp = await SearchAsync(missingSearch, includeDetails: false, pitId: "");
                    long missingCount = missResp.IsValid ? missResp.Total : 0;
                    var hits = new List<ViewResultDto>();
                    if (missingCount > 0)
                    {
                        hits.Add(new ViewResultDto { CategoryName = "(Uncategorized)", Count = missingCount, NotCategorized = true });
                    }
                    return new SearchResultDto<ViewResultDto> { Hits = hits, HitType = "view", ViewName = view };
                }
            }
            if (category == "(Uncategorized)")
            {
                var baseField = (viewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
                var missingFilter = new JObject(
                    new JProperty("bool", new JObject(
                        new JProperty("should", new JArray(
                            new JObject(
                                new JProperty("bool", new JObject(
                                    new JProperty("must_not", new JArray(
                                        new JObject(
                                            new JProperty("exists", new JObject(
                                                new JProperty("field", baseField)
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
                string categoryQuery = string.IsNullOrEmpty(viewDto.CategoryQuery) ? $"{viewDto.Aggregation}:\"{category}\"" : viewDto.CategoryQuery.Replace("{}", category);
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
                        var viewSecondaryResult = await SearchWithElasticQueryAndViewAsync(elasticsearchQuery, secondaryViewDto, pageSize, from);
                        return new SearchResultDto<ViewResultDto> { Hits = viewSecondaryResult, HitType = "view", ViewName = view, viewCategory = category };
                    }
                    if (secondaryCategory == "(Uncategorized)")
                    {
                        isHitFromViewDrilldown = true;
                        var secondaryBaseField = (secondaryViewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
                        var secondaryMissing = new JObject(
                            new JProperty("bool", new JObject(
                                new JProperty("should", new JArray(
                                    new JObject(
                                        new JProperty("bool", new JObject(
                                            new JProperty("must_not", new JArray(
                                                new JObject(
                                                    new JProperty("exists", new JObject(
                                                        new JProperty("field", secondaryBaseField)
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
                        isHitFromViewDrilldown = true;
                        string secondaryCategoryQuery = string.IsNullOrEmpty(secondaryViewDto.CategoryQuery) ? $"{secondaryViewDto.Aggregation}:\"{secondaryCategory}\"" : secondaryViewDto.CategoryQuery.Replace("{}", secondaryCategory);
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
            catch
            {
                // Absolute fallback for view path: return safe view response with only (Uncategorized) computed
                var baseField = "fname"; // for First Name default
                var missingQuery = new JObject(
                    new JProperty("bool", new JObject(
                        new JProperty("must", new JArray(
                            elasticsearchQuery,
                            new JObject(
                                new JProperty("bool", new JObject(
                                    new JProperty("must_not", new JArray(
                                        new JObject(new JProperty("exists", new JObject(new JProperty("field", baseField))))
                                    ))
                                ))
                            )
                        ))
                    ))
                );
                var missingSearch = new SearchRequest<Birthday>
                {
                    Size = 0,
                    Query = new QueryContainerDescriptor<Birthday>().Raw(missingQuery.ToString())
                };
                var missResp = await SearchAsync(missingSearch, includeDetails: false, pitId: "");
                long missingCount = missResp.IsValid ? missResp.Total : 0;
                var hits = new List<ViewResultDto>();
                if (missingCount > 0)
                {
                    hits.Add(new ViewResultDto { CategoryName = "(Uncategorized)", Count = missingCount, NotCategorized = true });
                }
                return new SearchResultDto<ViewResultDto> { Hits = hits, HitType = "view", ViewName = view };
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
        sortDescriptor.Add(new FieldSort { Field = "_id", Order = SortOrder.Ascending });

        searchRequest.Query = new QueryContainerDescriptor<Birthday>().Raw(elasticsearchQuery.ToString());
        searchRequest.Sort = sortDescriptor;
        var response = await SearchAsync(searchRequest, includeDetails, pitId);

        var birthdayDtos = new List<BirthdayDto>();
        foreach (var hit in response.Hits)
        {
            var b = hit.Source;
            var dto = new BirthdayDto
            {
                Id = b.Id!,
                Lname = b.Lname,
                Fname = b.Fname,
                Sign = b.Sign,
                Dob = b.Dob,
                IsAlive = b.IsAlive,
                Text = b.Text,
                Wikipedia = includeDetails ? b.Wikipedia : null,
                Categories = b.Categories
            };
            // Normalize display only: distinct case-insensitive; single-letter categories uppercase
            if (dto.Categories != null && dto.Categories.Count > 0)
            {
                dto.Categories = dto.Categories
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(s => s.Length == 1 ? s.ToUpperInvariant() : s)
                    .ToList();
            }
            birthdayDtos.Add(dto);
        }
        List<object>? searchAfterResponse = response.Hits.Count > 0 ? response.Hits.Last().Sorts.ToList() : null;
        var hitType = isHitFromViewDrilldown ? "hit" : "birthday";
        return new SearchResultDto<BirthdayDto>
        {
            Hits = birthdayDtos,
            TotalHits = response.Total,
            HitType = hitType,
            PitId = searchRequest.PointInTime?.Id,
            searchAfter = searchAfterResponse
        };
    }

    public async Task<BirthdayDto?> GetByIdAsync(string id, bool includeDetails = false)
    {
        var searchRequest = new SearchRequest<Birthday>
        {
            Query = new QueryContainerDescriptor<Birthday>().Term(t => t.Field("_id").Value(id))
        };
        var response = await SearchAsync(searchRequest, includeDetails: true, "");
        if (!response.IsValid || !response.Documents.Any())
        {
            return null;
        }
        var birthday = response.Documents.First();
        var dto = new BirthdayDto
        {
            Id = id,
            Text = birthday.Text,
            Lname = birthday.Lname,
            Fname = birthday.Fname,
            Sign = birthday.Sign,
            Dob = birthday.Dob,
            IsAlive = birthday.IsAlive ?? false,
            Wikipedia = includeDetails ? birthday.Wikipedia : null,
            Categories = birthday.Categories
        };
        return dto;
    }
}
