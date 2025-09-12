using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Nest;
using Elasticsearch.Net;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using JhipsterSampleApplication.Dto;
using System.IO;
using System.Text;
using System.Net;

namespace JhipsterSampleApplication.Domain.Services;

/// <summary>
/// Service for interacting with Elasticsearch for Entity operations
/// </summary>
public class EntityService<T> : IEntityService<T> where T : class
{
    private readonly ElasticLowLevelClient _elasticClient;
    private readonly string _indexName = "";
    private readonly string _detailFields = "";
    private readonly IBqlService<T> _bqlService;
    private readonly IViewService _viewService;

    /// <summary>
    /// Initializes a new instance of the EntityService
    /// </summary>
    /// <param name="elasticClient">The Elasticsearch client</param>
    /// <param name="bqlService">The BQL service</param>
    /// <param name="viewService">The View service</param>
    public EntityService(string indexName, string detailFields, ElasticLowLevelClient elasticClient, IBqlService<T> bqlService, IViewService viewService)
    {
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _detailFields = detailFields ?? throw new ArgumentNullException(nameof(detailFields));
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
        _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
    }

    /// <summary>
    /// Searches for Entity documents using a raw request body
    /// </summary>
    /// <param name="request">An object representing the search request body</param>
    /// <returns>The search response containing Entity documents</returns>
    public async Task<SearchResponse<T>> SearchAsync(object request, bool includeDetails, string? pitId = null)
    {
        var body = request != null ? JObject.FromObject(request) : new JObject();

        if (!includeDetails)
        {
            body["_source"] = new JObject
            {
                ["excludes"] = new JArray(_detailFields.Split(','))
            };
        }

        if (pitId == null)
        {
            var pitResponse = await _elasticClient.OpenPointInTimeAsync<StringResponse>(_indexName, new OpenPointInTimeRequestParameters { KeepAlive = "2m" });
            if (pitResponse.ApiCall.HttpStatusCode != (int)HttpStatusCode.OK)
            {
                throw new Exception($"Failed to open point in time: {pitResponse.Body}");
            }
            var pitJson = JObject.Parse(pitResponse.Body ?? "{}");
            pitId = pitJson.Value<string>("id");
        }

        if (!string.IsNullOrEmpty(pitId))
        {
            body["pit"] = new JObject { ["id"] = pitId, ["keep_alive"] = "2m" };
        }

        var response = await _elasticClient.SearchAsync<SearchResponse<T>>(_indexName, PostData.Serializable(body));
        if (response.ApiCall.HttpStatusCode >= 400)
        {
            var retry = await _elasticClient.SearchAsync<StringResponse>(_indexName, PostData.Serializable(body), new SearchRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
            throw new Exception(retry.Body ?? response.ApiCall.OriginalException?.Message);
        }

        foreach (var hit in response.Hits)
        {
            if (hit.Source != null)
            {
                var source = (CategorizedEntity<string>)(object)hit.Source;
                source.Id = hit.Id;
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

        var requestBody = new Dictionary<string, object>
        {
            ["from"] = from,
            ["size"] = 0,
            ["query"] = JObject.Parse(query)
        };

        var terms = string.IsNullOrEmpty(viewDto.Script)
            ? new Dictionary<string, object> { ["field"] = viewDto.Aggregation, ["size"] = 10000 }
            : new Dictionary<string, object> { ["script"] = new { source = viewDto.Script }, ["size"] = 10000 };

        requestBody["aggs"] = new Dictionary<string, object>
        {
            ["distinct"] = new Dictionary<string, object> { ["terms"] = terms }
        };

        var searchAggResponse = await _elasticClient.SearchAsync<SearchResponse<T>>(_indexName, PostData.Serializable(requestBody));

        var bucketAggregate = searchAggResponse.Aggregations?["distinct"] as BucketAggregate;
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
        var uncategorizedBody = new Dictionary<string, object>
        {
            ["from"] = from,
            ["size"] = 0,
            ["query"] = JObject.Parse(query),
            ["aggs"] = new Dictionary<string, object>
            {
                ["uncategorized"] = new Dictionary<string, object>
                {
                    ["filter"] = new Dictionary<string, object>
                    {
                        ["bool"] = new Dictionary<string, object>
                        {
                            ["must_not"] = new object[]
                            {
                                new Dictionary<string, object>
                                {
                                    ["exists"] = new Dictionary<string, object> { ["field"] = baseField }
                                }
                            }
                        }
                    }
                }
            }
        };

        var uncategorizedResponse = await _elasticClient.SearchAsync<SearchResponse<T>>(_indexName, PostData.Serializable(uncategorizedBody));

        long uncatCount = 0;
        if (uncategorizedResponse?.Aggregations != null)
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
    public async Task<List<ViewResultDto>> SearchUsingViewAsync(object request, object uncategorizedRequest)
    {
        List<ViewResultDto> content = new();

        var result = await _elasticClient.SearchAsync<SearchResponse<T>>(_indexName, PostData.Serializable(request));
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

        var uncategorizedResponse = await _elasticClient.SearchAsync<SearchResponse<T>>(_indexName, PostData.Serializable(uncategorizedRequest));
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


    /// <summary>
    /// Indexes a new Entity document
    /// </summary>
    /// <param name="Entity">The Entity document to index</param>
    /// <returns>The index response</returns>
    public async Task<IndexResponse> IndexAsync(T entity)
    {
        var response = await _elasticClient.IndexAsync<IndexResponse>(_indexName, PostData.Serializable(entity));
        if (response.ApiCall.HttpStatusCode >= 400)
        {
            var retry = await _elasticClient.IndexAsync<StringResponse>(_indexName, PostData.Serializable(entity), new IndexRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
            throw new Exception(retry.Body ?? response.ApiCall.OriginalException?.Message);
        }
        return response;
    }

    /// <summary>
    /// Updates an existing Entity document
    /// </summary>
    /// <param name="id">The ID of the document to update</param>
    /// <param name="Entity">The updated Entity document</param>
    /// <returns>The update response</returns>
    public async Task<UpdateResponse<T>> UpdateAsync(string id, T entity)
    {
        var payload = new { doc = entity, doc_as_upsert = true };
        var response = await _elasticClient.UpdateAsync<UpdateResponse<T>>(_indexName, id, PostData.Serializable(payload));
        if (response.ApiCall.HttpStatusCode >= 400)
        {
            var retry = await _elasticClient.UpdateAsync<StringResponse>(_indexName, id, PostData.Serializable(payload), new UpdateRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
            throw new Exception(retry.Body ?? response.ApiCall.OriginalException?.Message);
        }
        return response;
    }

    /// <summary>
    /// Deletes a entity document
    /// </summary>
    /// <param name="id">The ID of the document to delete</param>
    /// <returns>The delete response</returns>
    public async Task<DeleteResponse> DeleteAsync(string id)
    {
        var response = await _elasticClient.DeleteAsync<DeleteResponse>(_indexName, id);
        if (response.ApiCall.HttpStatusCode >= 400)
        {
            var retry = await _elasticClient.DeleteAsync<StringResponse>(_indexName, id, new DeleteRequestParameters { RequestConfiguration = new RequestConfiguration { DisableDirectStreaming = true } });
            throw new Exception(retry.Body ?? response.ApiCall.OriginalException?.Message);
        }
        return response;
    }

    /// <summary>
    /// Gets unique values for a field in the entity index
    /// </summary>
    /// <param name="field">The field to get unique values for</param>
    /// <returns>A collection of unique field values</returns>
    public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
    {
        var body = new
        {
            size = 0,
            aggs = new
            {
                distinct = new
                {
                    terms = new { field = field, size = 10000 }
                }
            }
        };
        var result = await _elasticClient.SearchAsync<SearchResponse<object>>(_indexName, PostData.Serializable(body));
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
    public async Task<SearchResponse<T>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0)
    {
        var queryObject = await ConvertRulesetToElasticSearch(ruleset);
        var body = new Dictionary<string, object>
        {
            ["size"] = size,
            ["from"] = from,
            ["query"] = queryObject,
            ["sort"] = new object[] { new Dictionary<string, object> { ["_id"] = new { order = "asc" } } }
        };

        return await SearchAsync(body, false);
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

    public async Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto viewDto, int size = 20, int from = 0)
    {
        string query = queryObject.ToString();        
        return await SearchUsingViewAsync(query, viewDto, from, size);
    }

    public async Task<SimpleApiResponse> CategorizeAsync(CategorizeRequestDto request)
    {
        var searchBody = new
        {
            query = new
            {
                terms = new Dictionary<string, object> { ["_id"] = request.Ids }
            }
        };
        var response = await SearchAsync(searchBody, true, "");
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
        await _elasticClient.Indices.RefreshAsync<StringResponse>(_indexName);
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
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var toRemove = (request.Remove ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!toAdd.Any() && !toRemove.Any())
        {
            return new SimpleApiResponse { Success = false, Message = "Nothing to add or remove" };
        }

        var searchBody = new
        {
            query = new
            {
                terms = new Dictionary<string, object> { ["_id"] = request.Rows }
            }
        };
        var response = await SearchAsync(searchBody, true, "");
        if (!response.IsValid)
        {
            return new SimpleApiResponse { Success = false, Message = "Failed to search for Entitys" };
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

        await _elasticClient.Indices.RefreshAsync<StringResponse>(_indexName);
        return new SimpleApiResponse
        {
            Success = errorCount == 0,
            Message = message
        };
    }
}
