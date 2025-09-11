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
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace JhipsterSampleApplication.Domain.Services;

/// <summary>
/// Service for interacting with Elasticsearch for Birthday operations
/// </summary>
public class EntityService<T> : IEntityService<T> where T : class
{
    private readonly IElasticClient _elasticClient;
    private readonly string _indexName = "";
    private readonly string _detailFields = "";
    private readonly IBqlService<T> _bqlService;
    private readonly IViewService _viewService;

    /// <summary>
    /// Initializes a new instance of the BirthdayService
    /// </summary>
    /// <param name="elasticClient">The Elasticsearch client</param>
    /// <param name="bqlService">The BQL service</param>
    /// <param name="viewService">The View service</param>
    public EntityService(string indexName, string detailFields, IElasticClient elasticClient, IBqlService<T> bqlService, IViewService viewService)
    {
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _detailFields = detailFields ?? throw new ArgumentNullException(nameof(detailFields));
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
        _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
    }

    /// <summary>
    /// Searches for Birthday documents using the provided search request
    /// </summary>
    /// <param name="request">The search request to execute</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<T>> SearchAsync(ISearchRequest request, bool includeDetails, string? pitId = null) 
    {            
        if (!includeDetails)
        {
            request.Source = new SourceFilter { Excludes = _detailFields.Split(',') };
        }
        if (pitId == null)
        {
            var pitResponse = await _elasticClient.OpenPointInTimeAsync(new OpenPointInTimeRequest(_indexName)
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
        var response = await _elasticClient.SearchAsync<T>(request);
        if (!response.IsValid)
        {
            var status = response.ApiCall?.HttpStatusCode ?? 0;
            if (status >= 400)
            {
                // Retry with direct streaming disabled to expose detailed error information
                StringResponse retryResponse;
                if (request.PointInTime != null)
                {
                    retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
                }
                else
                {
                    retryResponse = await _elasticClient.LowLevel.SearchAsync<StringResponse>(_indexName, PostData.Serializable(request), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
                }
                throw new Exception(retryResponse.Body ?? response.ServerError?.ToString());
            }
            // Otherwise, allow the response to pass through (avoid 500 on benign conditions)
        }
        if (response.Hits.Count > 0)
        {
            foreach (var hit in response.Hits)
            {
                if (hit.Source != null)
                {
                    var source = (CategorizedEntity<string>)(object)hit.Source;
                    source.Id = hit.Id;
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
    public async Task<List<ViewResultDto>> SearchUsingViewAsync(string query, ViewDto viewDto, int from, int size)
    {
        List<ViewResultDto> content = new();

        // Primary aggregation over the selected view
        var searchAggResponse = await _elasticClient.SearchAsync<T>(s =>
            s.Index(_indexName)
             .From(from)
             .Size(0)
             .Query(q => q.Raw(query))
             .Aggregations(a => a
                .Terms("distinct", t =>
                    string.IsNullOrEmpty(viewDto.Script)
                        ? t.Field(viewDto.Aggregation).Size(10000)
                        : t.Script(ss => ss.Source(viewDto.Script)).Size(10000)
                ))
        );

        var bucketAggregate = searchAggResponse.IsValid ? (searchAggResponse.Aggregations["distinct"] as BucketAggregate) : null;
        if (bucketAggregate != null)
        {
            foreach (var it in bucketAggregate.Items.OfType<KeyedBucket<object>>())
            {
                string categoryName = it.KeyAsString ?? (it.Key?.ToString() ?? string.Empty);
                bool notCategorized = false;
                if (Regex.IsMatch(categoryName, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.\d{3}Z"))
                {
                    categoryName = Regex.Replace(categoryName, @"(\d{4})-(\d{2})-(\d{2})T\d{2}:\d{2}:\d{2}.\d{3}Z", "$1-$2-$3");
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
        }

        content = content.OrderBy(cat => cat.CategoryName).ToList();

        // Count uncategorized explicitly (missing or empty)
        var baseField = (viewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
        var uncategorizedResponse = await _elasticClient.SearchAsync<T>(s =>
            s.Index(_indexName)
             .From(from)
             .Size(0)
             .Query(q => q.Raw(query))
             .Aggregations(a => a
                .Filter("uncategorized", f => f
                    .Filter(b => b.Bool(bl => bl.MustNot(mn => mn.Exists(e => e.Field(baseField)))))
                )
             )
        );

        long uncatCount = 0;
        if (uncategorizedResponse != null && uncategorizedResponse.IsValid && uncategorizedResponse.Aggregations != null)
        {
            var filterAgg = uncategorizedResponse.Aggregations.Filter("uncategorized");
            if (filterAgg != null)
            {
                uncatCount = filterAgg.DocCount;
            }
        }
        if (uncatCount > 0)
        {
            var existingUncategorized = content.FirstOrDefault(c => c.NotCategorized == true);
            if (existingUncategorized != null)
            {
                existingUncategorized.Count = (existingUncategorized.Count ?? 0) + uncatCount;
            }
            else
            {
                content.Add(new ViewResultDto
                {
                    CategoryName = "(Uncategorized)",
                    Selected = false,
                    NotCategorized = true,
                    Count = uncatCount
                });
            }
        }
        return content;
    }

    // Back-compat implementation to satisfy interface; not used by new flow but kept for callers
    public async Task<List<ViewResultDto>> SearchUsingViewAsync(ISearchRequest request, ISearchRequest uncategorizedRequest)
    {
        List<ViewResultDto> content = new();

        var result = await _elasticClient.SearchAsync<T>(request);
        var distinctAgg = result.Aggregations?["distinct"] as BucketAggregate;
        if (distinctAgg != null)
        {
            foreach (var it in distinctAgg.Items.OfType<KeyedBucket<object>>())
            {
                string categoryName = it.KeyAsString ?? (it.Key?.ToString() ?? string.Empty);
                bool notCategorized = false;
                if (Regex.IsMatch(categoryName, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.\d{3}Z"))
                {
                    categoryName = Regex.Replace(categoryName, @"(\d{4})-(\d{2})-(\d{2})T\d{2}:\d{2}:\d{2}.\d{3}Z", "$1-$2-$3");
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
        }

        content = content.OrderBy(cat => cat.CategoryName).ToList();

        var uncategorizedResponse = await _elasticClient.SearchAsync<T>(uncategorizedRequest);
        var uncatCount = uncategorizedResponse.Aggregations.Filter("uncategorized").DocCount;
        if (uncatCount > 0)
        {
            var existingUncategorized = content.FirstOrDefault(c => c.NotCategorized == true);
            if (existingUncategorized != null)
            {
                existingUncategorized.Count = (existingUncategorized.Count ?? 0) + uncatCount;
            }
            else
            {
                content.Add(new ViewResultDto
                {
                    CategoryName = "(Uncategorized)",
                    Selected = false,
                    NotCategorized = true,
                    Count = uncatCount
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
    public async Task<IndexResponse> IndexAsync(T entity)
    {
        return await _elasticClient.IndexAsync(entity, i => i.Index(_indexName).Refresh(Refresh.WaitFor));
    }

    /// <summary>
    /// Updates an existing Birthday document
    /// </summary>
    /// <param name="id">The ID of the document to update</param>
    /// <param name="birthday">The updated Birthday document</param>
    /// <returns>The update response</returns>
    public async Task<UpdateResponse<T>> UpdateAsync(string id, T entity)
    {
        return await _elasticClient.UpdateAsync<T>(id, u => u
            .Index(_indexName)
            .Doc(entity)
            .DocAsUpsert()
            .Refresh(Refresh.WaitFor)
        );
    }

    /// <summary>
    /// Deletes a entity document
    /// </summary>
    /// <param name="id">The ID of the document to delete</param>
    /// <returns>The delete response</returns>
    public async Task<DeleteResponse> DeleteAsync(string id)
    {
        return await _elasticClient.DeleteAsync<T>(id);
    }

    /// <summary>
    /// Gets unique values for a field in the entity index
    /// </summary>
    /// <param name="field">The field to get unique values for</param>
    /// <returns>A collection of unique field values</returns>
    public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
    {
        var result = await _elasticClient.SearchAsync<Aggregation>(q => q
            .Size(0).Index(_indexName).Aggregations(agg => agg.Terms(
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
    /// Searches for entity documents using a ruleset
    /// </summary>
    /// <param name="ruleset">The ruleset to use for searching</param>
    /// <param name="size">The maximum number of results to return</param>
    /// <param name="from">The starting index for pagination</param>
    /// <param name="sort">The sort descriptor for the search</param>
    /// <returns>The search response containing entity documents</returns>
    public async Task<ISearchResponse<T>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null)
    {
        var queryObject = await ConvertRulesetToElasticSearch(ruleset);
        string query = queryObject.ToString();
        var searchRequest = new SearchRequest<T>
        {
            Size = size,
            From = from,
            Query = new QueryContainerDescriptor<T>().Raw(query)
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

        return await SearchAsync(searchRequest, false);     
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
        return await SearchUsingViewAsync(query, viewDto, from, size);
    }

    public async Task<SimpleApiResponse> CategorizeAsync(CategorizeRequestDto request)
    {
        var searchRequest = new SearchRequest<T>
        {
            Query = new QueryContainerDescriptor<T>().Terms(t => t.Field("_id").Terms(request.Ids))
        };
        var response = await SearchAsync(searchRequest, true, "");
        if (!response.IsValid)
        {
            return new SimpleApiResponse { Success = false, Message = "Failed to search for the entities" };
        }
        var successCount = 0;
        var errorCount = 0;
        var errorMessages = new List<string>();

        foreach (var genericEntity in response.Documents)
        {
            var entity = (CategorizedEntity<string>)(object)genericEntity;
            try
            {
                if (request.RemoveCategory)
                {
                    if (entity!.Categories != null)
                    {
                        var categoryToRemove = entity.Categories.FirstOrDefault(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase));
                        if (categoryToRemove != null)
                        {
                            entity.Categories.Remove(categoryToRemove);
                            var updateResponse = await UpdateAsync(entity.Id!, (T)(object)entity);
                            if (updateResponse.IsValid)
                            {
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                                errorMessages.Add($"Failed to update entity {entity.Id}: {updateResponse.DebugInformation}");
                            }
                        }
                    }
                }
                else
                {
                    if (entity.Categories == null)
                    {
                        entity.Categories = new List<string>();
                    }
                    if (!entity.Categories.Any(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase)))
                    {
                        entity.Categories.Add(request.Category);
                        var updateResponse = await UpdateAsync(entity.Id!, (T)(object)entity);
                        if (updateResponse.IsValid)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errorMessages.Add($"Failed to update entity {entity.Id}: {updateResponse.DebugInformation}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                errorMessages.Add($"Error processing entity {entity.Id}: {ex.Message}");
            }
        }

        var message = $"Processed {request.Ids.Count} entities. Success: {successCount}, Errors: {errorCount}";
        if (errorMessages.Any())
        {
            message += $". Error details: {string.Join("; ", errorMessages)}";
        }
        await _elasticClient.Indices.RefreshAsync(_indexName);
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

        var searchRequest = new SearchRequest<T>
        {
            Query = new QueryContainerDescriptor<T>().Terms(t => t.Field("_id").Terms(request.Rows))
        };
        var response = await SearchAsync(searchRequest, true, "");
        if (!response.IsValid)
        {
            return new SimpleApiResponse { Success = false, Message = "Failed to search for birthdays" };
        }

        var successCount = 0;
        var errorCount = 0;
        var errorMessages = new List<string>();

        foreach (var genericEntity in response.Documents)
        {
            var entity = (CategorizedEntity<string>)(object)genericEntity;
            try
            {
                var current = entity!.Categories ?? new List<string>();
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

                entity.Categories = current;
                var updateResponse = await UpdateAsync(entity.Id!, (T)(object)entity);
                if (updateResponse.IsValid)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    errorMessages.Add($"Failed to update entity {entity.Id}: {updateResponse.DebugInformation}");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                errorMessages.Add($"Error processing entity {entity.Id}: {ex.Message}");
            }
        }

        var message = $"Processed {request.Rows.Count} entities. Success: {successCount}, Errors: {errorCount}";
        if (errorMessages.Any())
        {
            message += $". Error details: {string.Join("; ", errorMessages)}";
        }

        await _elasticClient.Indices.RefreshAsync(_indexName);
        return new SimpleApiResponse
        {
            Success = errorCount == 0,
            Message = message
        };
    }
}
