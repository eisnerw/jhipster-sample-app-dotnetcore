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
public class BirthdayService : IBirthdayService
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "birthdays";
    private readonly IBirthdayBqlService _bqlService;
    private readonly IViewService _viewService;

    /// <summary>
    /// Initializes a new instance of the BirthdayService
    /// </summary>
    /// <param name="elasticClient">The Elasticsearch client</param>
    /// <param name="bqlService">The BQL service</param>
    /// <param name="viewService">The View service</param>
    public BirthdayService(IElasticClient elasticClient, IBirthdayBqlService bqlService, IViewService viewService)
    {
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
        _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
    }

    /// <summary>
    /// Searches for Birthday documents using the provided search request
    /// </summary>
    /// <param name="request">The search request to execute</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<Birthday>> SearchAsync(ISearchRequest request, string? pitId = null) 
    {
        if (pitId == null)
        {
            var pitResponse = await _elasticClient.OpenPointInTimeAsync(new OpenPointInTimeRequest(IndexName)
            {
                KeepAlive = "2m" // Set the keep-alive duration for the PIT
            });
            if (!pitResponse.IsValid)
            {
                throw new Exception($"Failed to open point in time: {pitResponse.DebugInformation}");
            }
            pitId = pitResponse.Id;
        }
        if (!string.IsNullOrEmpty(pitId))
        {
            // Note: Not setting PIT in the request if pitId is empty string
            request.PointInTime = new PointInTime(pitId);
        }
        var response = await _elasticClient.SearchAsync<Birthday>(request);
        if (!response.IsValid)
        {
            // Retry with direct streaming disabled to expose detailed error information
            StringResponse retryResponse;
            if (request.PointInTime != null)
            {
                // When using PIT, do not specify an index in the path
                retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
            }
            else
            {
                retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(IndexName, PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
            }
            throw new Exception(retryResponse.Body);
        }
        if (response.Hits.Count > 0)
        {
            foreach (var hit in response.Hits)
            {
                if (hit.Source != null)
                {
                    hit.Source.Id = hit.Id;
                }
            }
        }
        return response;
    }


    /// <summary>
    /// Searches for ViewResults by performing an aggregation using the provided search request
    /// </summary>
    /// <param name="request">The aggregation request to execute</param>
    /// <returns>The search response containing ViewResults</returns>
    public async Task<List<ViewResultDto>> SearchUsingViewAsync(ISearchRequest request, ISearchRequest uncategorizedRequest)
    {
        List<ViewResultDto> content = new();
        var result = await _elasticClient.SearchAsync<Aggregation>(request);
        // DEBUG var curl = ToCurl(_elasticClient, result, request);  // inspect `curl` in debugger
        // DEBUG System.Console.WriteLine(curl);
        var aggList = result.Aggregations?.ToList();
        if (aggList == null || aggList.Count == 0)
        {
            return content;
        }
        var bucketAggregate = aggList[0].Value as BucketAggregate;
        if (bucketAggregate == null)
        {
            return content;
        }
        foreach (var it in bucketAggregate.Items.OfType<KeyedBucket<object>>())
        {
            string categoryName = it.KeyAsString ?? (it.Key?.ToString() ?? string.Empty);
            bool notCategorized = false;
            if (Regex.IsMatch(categoryName, @"\d{4,4}-\d{2,2}-\d{2,2}T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z"))
            {
                categoryName = Regex.Replace(categoryName, @"(\d{4,4})-(\d{2,2})-(\d{2,2})T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z", "$1-$2-$3");
            }
            if (string.IsNullOrEmpty(categoryName))
            {
                categoryName = "(Uncategorized)";
                notCategorized = true;
            }
            content.Add(new ViewResultDto
            {
                CategoryName = categoryName,
                Count = it.DocCount,
                NotCategorized = notCategorized
            });
        }
        content = content.OrderBy(cat => cat.CategoryName).ToList();
        var uncategorizedResponse = await _elasticClient.SearchAsync<Birthday>(uncategorizedRequest);
        var uncatetgorizedCount = uncategorizedResponse.Aggregations.Filter("uncategorized").DocCount;
        if (uncatetgorizedCount > 0)
        {
            var existingUncategorized = content.FirstOrDefault(c => c.NotCategorized == true);
            if (existingUncategorized != null)
            {
                existingUncategorized.Count = (existingUncategorized.Count ?? 0) + uncatetgorizedCount;
            }
            else
            {
                content.Add(new ViewResultDto
                {
                    CategoryName = "(Uncategorized)",
                    Selected = false,
                    NotCategorized = true,
                    Count = uncatetgorizedCount
                });
            }
        }
        return content;
    }

    static string ToCurl(IElasticClient client, IResponse resp, ISearchRequest? originalRequest)
    {
        // Prefer the actual URL NEST hit; fall back to something sane if missing.
        var url = resp?.ApiCall?.Uri?.ToString() ?? "http://localhost:9200/_search";

        // 1) If NEST captured the body, use it…
        string? body = null;
        var bytes = resp?.ApiCall?.RequestBodyInBytes;
        if (bytes is { Length: > 0 })
            body = Encoding.UTF8.GetString(bytes);

        // 2) …otherwise, serialize the original request ourselves.
        if (string.IsNullOrWhiteSpace(body) && originalRequest != null)
        {
            using var ms = new MemoryStream();
            client.RequestResponseSerializer.Serialize(originalRequest, ms, SerializationFormatting.Indented);
            body = Encoding.UTF8.GetString(ms.ToArray());
        }

        // Escape for: curl -d '...'
        static string Esc(string s) => s.Replace("'", "'\"'\"'");

        // Use POST since we’re sending a body (ES accepts POST for _search).
        return $"curl -X POST \"{url}\" -H 'Content-Type: application/json' -d '{Esc(body ?? "{}")}'";
    }

    /// <summary>
    /// Indexes a new Birthday document
    /// </summary>
    /// <param name="birthday">The Birthday document to index</param>
    /// <returns>The index response</returns>
    public async Task<IndexResponse> IndexAsync(Birthday birthday)
    {
        return await _elasticClient.IndexDocumentAsync(birthday);
    }

    /// <summary>
    /// Updates an existing Birthday document
    /// </summary>
    /// <param name="id">The ID of the document to update</param>
    /// <param name="birthday">The updated Birthday document</param>
    /// <returns>The update response</returns>
    public async Task<UpdateResponse<Birthday>> UpdateAsync(string id, Birthday birthday)
    {
        return await _elasticClient.UpdateAsync<Birthday>(id, u => u
            .Doc(birthday)
            .DocAsUpsert()
        );
    }

    /// <summary>
    /// Deletes a Birthday document
    /// </summary>
    /// <param name="id">The ID of the document to delete</param>
    /// <returns>The delete response</returns>
    public async Task<DeleteResponse> DeleteAsync(string id)
    {
        return await _elasticClient.DeleteAsync<Birthday>(id);
    }

    /// <summary>
    /// Gets unique values for a field in the Birthday index
    /// </summary>
    /// <param name="field">The field to get unique values for</param>
    /// <returns>A collection of unique field values</returns>
    public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
    {
        var result = await _elasticClient.SearchAsync<Aggregation>(q => q
            .Size(0).Index("birthdays").Aggregations(agg => agg.Terms(
                "distinct", e =>
                    e.Field(field).Size(10000)
                )
            )
        );
        List<string> ret = new List<string>();
        if (result.Aggregations != null && result.Aggregations.Any())
        {
            var firstAggregation = result.Aggregations.First();
            if (firstAggregation.Value is BucketAggregate bucketAggregate && bucketAggregate.Items != null)
            {
                foreach (var item in bucketAggregate.Items)
                {
                    if (item is KeyedBucket<Object> kb)
                    {
                        string value = kb.KeyAsString ?? kb.Key?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(value))
                        {
                            ret.Add(value);
                        }
                    }
                }
            }
        }
        return ret;
    }

    /// <summary>
    /// Searches for Birthday documents using a ruleset
    /// </summary>
    /// <param name="ruleset">The ruleset to use for searching</param>
    /// <param name="size">The maximum number of results to return</param>
    /// <param name="from">The starting index for pagination</param>
    /// <param name="sort">The sort descriptor for the search</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<Birthday>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null)
    {
        var queryObject = await ConvertRulesetToElasticSearch(ruleset);
        string query = queryObject.ToString();
        var searchRequest = new SearchRequest<Birthday>
        {
            Size = size,
            From = from,
            Query = new QueryContainerDescriptor<Birthday>().Raw(query)
        };

        if (sort != null && sort.Any())
        {
            searchRequest.Sort = sort;
        }
        else
        {
            // Default sort by _id if no sort is provided
            searchRequest.Sort = new List<ISort>
            {
                new FieldSort { Field = "_id", Order = SortOrder.Ascending }
            };
        }

        return await SearchAsync(searchRequest);     
    }

    /// <summary>
    /// Converts a ruleset to an Elasticsearch query
    /// </summary>
    /// <param name="rr">The ruleset to convert</param>
    /// <returns>A JObject containing the Elasticsearch query</returns>
    
/// <summary>
/// Converts a ruleset to an Elasticsearch query
/// </summary>
/// <param name="rr">The ruleset to convert</param>
/// <returns>A JObject containing the Elasticsearch query</returns>
public async Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr)
{
    if (!ValidateRuleset(rr))
    {
        throw new ArgumentException("Invalid ruleset", nameof(rr));
    }

    var dto = MapToDto(rr);
    var result = await _bqlService.Ruleset2ElasticSearch(dto);
    return result is JObject jo ? jo : JObject.FromObject(result);
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

/// <summary>
    /// Searches for ViewResults using a ruleset and a view name
    /// </summary>
    /// <param name="ruleset">The ruleset to use for searching</param>
    /// <param name="view">The name of the view</param>
    /// <param name="size">The maximum number of results to return</param>
    /// <param name="from">The starting index for pagination</param>
    /// <param name="sort">The sort descriptor for the search</param>
    /// <returns>The search response containing a list of ViewResultDtos</returns>

    public async Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto viewDto, int size = 20, int from = 0, IList<ISort>? sort = null)
    {
        string query = queryObject.ToString();        
        var request = new SearchRequest<Birthday>
        {
            Size = 0,
            From = from,
            Query = new QueryContainerDescriptor<Birthday>().Raw(query),
            Aggregations = new AggregationDictionary{
                {
                    "distinct",
                    new TermsAggregation("distinct")
                    {
                        Size = 10000,
                        Field = string.IsNullOrEmpty(viewDto.Script) ? viewDto.Aggregation : null,
                        Script = !string.IsNullOrEmpty(viewDto.Script) ? new InlineScript(viewDto.Script) : null
                    }
                }
            }
        };
        var uncategorizedRequest = new SearchRequest<Birthday>
        {
            Size = 0,
            From = from,
            Query = new QueryContainerDescriptor<Birthday>().Raw(query),
            Aggregations = new AggregationDictionary{
                {
                    "uncategorized", new FilterAggregation("uncategorized")
                    {
                        Filter = new BoolQuery
                        {
                            Should = new List<QueryContainer>
                            {
                                new BoolQuery
                                {
                                    MustNot = new List<QueryContainer>
                                    {
                                        new ExistsQuery
                                        {
                                            Field = viewDto.Aggregation
                                        }
                                    }
                                },
                                new TermQuery
                                {
                                    Field = viewDto.Aggregation,
                                    Value = string.Empty
                                }
                            },
                            MinimumShouldMatch = 1
                        }
                    }

                }
            }
        };
        return await SearchUsingViewAsync(request, uncategorizedRequest);
    }

    public async Task<string?> GetHtmlByIdAsync(string id)
    {
        var searchRequest = new SearchRequest<Birthday>
        {
            Query = new QueryContainerDescriptor<Birthday>().Term(t => t.Field("_id").Value(id))
        };
        var response = await SearchAsync(searchRequest, "");
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
                var viewResult = await SearchWithElasticQueryAndViewAsync(elasticsearchQuery, viewDto, from, pageSize);
                return new SearchResultDto<ViewResultDto> { Hits = viewResult, HitType = "view", ViewName = view };
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
                    var viewSecondaryResult = await SearchWithElasticQueryAndViewAsync(elasticsearchQuery, secondaryViewDto, from, pageSize);
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
        var response = await SearchAsync(searchRequest, pitId);

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
                Wikipedia = includeDetails ? b.Wikipedia : null,
                Categories = b.Categories
            });
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
        var response = await SearchAsync(searchRequest, "");
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

    public async Task<SimpleApiResponse> CategorizeAsync(CategorizeRequestDto request)
    {
        var searchRequest = new SearchRequest<Birthday>
        {
            Query = new QueryContainerDescriptor<Birthday>().Terms(t => t.Field("_id").Terms(request.Ids))
        };
        var response = await SearchAsync(searchRequest, "");
        if (!response.IsValid)
        {
            return new SimpleApiResponse { Success = false, Message = "Failed to search for birthdays" };
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
                    if (birthday.Categories != null)
                    {
                        var categoryToRemove = birthday.Categories.FirstOrDefault(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase));
                        if (categoryToRemove != null)
                        {
                            birthday.Categories.Remove(categoryToRemove);
                            var updateResponse = await UpdateAsync(birthday.Id!, birthday);
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
                    if (birthday.Categories == null)
                    {
                        birthday.Categories = new List<string>();
                    }
                    if (!birthday.Categories.Any(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase)))
                    {
                        birthday.Categories.Add(request.Category);
                        var updateResponse = await UpdateAsync(birthday.Id!, birthday);
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
        return new SimpleApiResponse
        {
            Success = errorCount == 0,
            Message = message
        };
    }

    public async Task<SimpleApiResponse> CategorizeMultipleAsync(CategorizeMultipleRequestDto request)
    {
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
            return new SimpleApiResponse { Success = false, Message = "Nothing to add or remove" };
        }

        var searchRequest = new SearchRequest<Birthday>
        {
            Query = new QueryContainerDescriptor<Birthday>().Terms(t => t.Field("_id").Terms(request.Rows))
        };
        var response = await SearchAsync(searchRequest, "");
        if (!response.IsValid)
        {
            return new SimpleApiResponse { Success = false, Message = "Failed to search for birthdays" };
        }

        var successCount = 0;
        var errorCount = 0;
        var errorMessages = new List<string>();

        foreach (var birthday in response.Documents)
        {
            try
            {
                var current = birthday.Categories ?? new List<string>();
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

                birthday.Categories = current;
                var updateResponse = await UpdateAsync(birthday.Id!, birthday);
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
            catch (Exception ex)
            {
                errorCount++;
                errorMessages.Add($"Error processing birthday {birthday.Id}: {ex.Message}");
            }
        }

        var message = $"Processed {request.Rows.Count} birthdays. Success: {successCount}, Errors: {errorCount}";
        if (errorMessages.Any())
        {
            message += $". Error details: {string.Join("; ", errorMessages)}";
        }

        return new SimpleApiResponse
        {
            Success = errorCount == 0,
            Message = message
        };
    }
}
