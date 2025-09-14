using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Transport;
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
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using JhipsterSampleApplication.Domain.Search;
using Microsoft.Extensions.DependencyInjection;

namespace JhipsterSampleApplication.Domain.Services;

/// <summary>
/// Service for interacting with Elasticsearch for Entity operations
/// </summary>
public class EntityService<T> : IEntityService<T> where T : class
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly string _indexName = "";
    private readonly string _detailFields = "";
    private readonly IBqlService<T> _bqlService;
    private readonly IViewService _viewService;
    // Set this to true in the debugger to enable verbose ES request logging
    private static bool debug = false;
    private readonly IConfiguration? _configuration;

    /// <summary>
    /// Initializes a new instance of the EntityService
    /// </summary>
    /// <param name="elasticClient">The Elasticsearch client</param>
    /// <param name="bqlService">The BQL service</param>
    /// <param name="viewService">The View service</param>
    public EntityService(string indexName, string detailFields, IServiceProvider serviceProvider, IBqlService<T> bqlService, IViewService viewService)
    {
        _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        _detailFields = detailFields ?? throw new ArgumentNullException(nameof(detailFields));
        _elasticClient = serviceProvider.GetRequiredService<ElasticsearchClient>();
        _bqlService = bqlService ?? throw new ArgumentNullException(nameof(bqlService));
        _viewService = viewService ?? throw new ArgumentNullException(nameof(viewService));
        _configuration = serviceProvider.GetRequiredService<IConfiguration>();
    }

    private string NormalizeSortField(string field)
    {
        if (string.Equals(field, "_id", StringComparison.OrdinalIgnoreCase)) return field;
        if (string.Equals(_indexName, "movies", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(field, "title", StringComparison.OrdinalIgnoreCase)) return "title.keyword";
        }
        return field;
    }

    public async Task<AppSearchResponse<T>> SearchAsync(SearchSpec<T> spec)
    {
        // If caller supplied a raw JSON query, use HTTP raw call to preserve exact semantics
        if (spec.RawQuery != null)
            return await SearchRawAsync(spec);

        // Decide PIT first, then choose how to construct the request (typed index vs PIT-only).
        var pitId = spec.PitId;
        if (pitId == null)
        {
            var pitResponse = await _elasticClient.OpenPointInTimeAsync(new OpenPointInTimeRequest(_indexName)
            {
                KeepAlive = "2m"
            });
            if (pitResponse.IsValidResponse)
            {
                pitId = pitResponse.Id;
            }
        }

        var response = await _elasticClient.SearchAsync<T>(s =>
        {
            if (string.IsNullOrEmpty(pitId)) s.Index(_indexName);
            else s.Pit(p => p.Id(pitId).KeepAlive("2m"));

            if (spec.From.HasValue) s.From(spec.From.Value);
            if (spec.Size.HasValue) s.Size(spec.Size.Value);
            if (!spec.IncludeDetails) s.SourceExcludes(_detailFields.Split(','));

            if (!string.IsNullOrEmpty(spec.Id))
            {
                s.Query(q => q.Term(t => t.Field("_id").Value(spec.Id)));
            }
            else if (spec.Ids != null && spec.Ids.Count > 0)
            {
                s.Query(q => q.Ids(iq => iq.Values(new Ids(spec.Ids.Select(x => (Id)x).ToArray()))));
            }

            s.Sort(so =>
            {
                if (spec.Sorts != null && spec.Sorts.Count > 0)
                {
                    foreach (var sortSpec in spec.Sorts)
                    {
                        var order = string.Equals(sortSpec.Order, "desc", StringComparison.OrdinalIgnoreCase) ? SortOrder.Desc : SortOrder.Asc;
                        if (string.IsNullOrWhiteSpace(sortSpec.Script))
                        {
                            var f = NormalizeSortField(sortSpec.Field);
                            so.Field(fld => fld.Field(f).Order(order));
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(spec.Sort))
                {
                    var parts = spec.Sort.Contains(',') ? spec.Sort.Split(',') : spec.Sort.Split(':');
                    if (parts.Length == 2)
                    {
                        var order = parts[1].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase) ? SortOrder.Desc : SortOrder.Asc;
                        var f = NormalizeSortField(parts[0].Trim());
                        so.Field(fld => fld.Field(f).Order(order));
                    }
                }
                // Always add _id
                so.Field(fld => fld.Field("_id").Order(SortOrder.Asc));
            });

            if (spec.SearchAfter != null && spec.SearchAfter.Count > 0)
            {
                var sa = new List<FieldValue>();
                foreach (var o in spec.SearchAfter)
                {
                    switch (o)
                    {
                        case string ssa: sa.Add(ssa); break;
                        case long ll: sa.Add(ll); break;
                        case int ii: sa.Add((long)ii); break;
                        case double dd: sa.Add(dd); break;
                        default: sa.Add(o?.ToString() ?? string.Empty); break;
                    }
                }
                s.SearchAfter(sa);
                s.From((int?)null);
            }
        });

        // local uses global helper

        if (debug)
        {
            Console.WriteLine($"[ES] {typeof(T).Name} index='{_indexName}', pit='{pitId ?? "(none)"}', from={spec.From}, size={spec.Size}");
            Console.WriteLine($"[ES] took={response.Took} hits={response.Hits.Count}");
        }
        if (!response.IsValidResponse)
        {
            var body = response.ApiCallDetails?.ResponseBodyInBytes != null ? Encoding.UTF8.GetString(response.ApiCallDetails.ResponseBodyInBytes) : response.ElasticsearchServerError?.ToString();
            throw new Exception(body ?? "Elasticsearch search failed");
        }
        var app = new AppSearchResponse<T>
        {
            Total = response.Hits?.Count ?? 0,
            PointInTimeId = pitId,
            IsValid = response.IsValidResponse
        };
        if (response.Hits != null)
        {
            foreach (var h in response.Hits)
            {
                if (h.Source != null)
                {
                    try { ((CategorizedEntity<string>)(object)h.Source).Id = h.Id; } catch {}
                }
                app.Hits.Add(new AppHit<T>
                {
                    Id = h.Id,
                    Source = h.Source!,
                    Sorts = new List<object>()
                });
            }
        }
        return app;
    }

    private async Task<AppSearchResponse<T>> SearchRawAsync(SearchSpec<T> spec)
    {
        var url = _configuration?["Elasticsearch:Url"]?.TrimEnd('/') ?? "http://localhost:9200";
        var username = _configuration?["Elasticsearch:Username"];
        var password = _configuration?["Elasticsearch:Password"];

        // Ensure we have a PIT for the first page to anchor paging across updates/deletes
        string? pitId = spec.PitId;
        if (string.IsNullOrWhiteSpace(pitId))
        {
            try
            {
                var pitResponse = await _elasticClient.OpenPointInTimeAsync(new OpenPointInTimeRequest(_indexName)
                {
                    KeepAlive = "2m"
                });
                if (pitResponse.IsValidResponse)
                {
                    pitId = pitResponse.Id;
                }
            }
            catch
            {
                // If PIT open fails, proceed without PIT; paging stability may be reduced
            }
        }

        var usePit = !string.IsNullOrEmpty(pitId);
        var path = usePit ? "/_search" : $"/{_indexName}/_search";

        var root = new JObject();
        var hasSearchAfter = spec.SearchAfter != null && spec.SearchAfter.Count > 0;
        if (spec.From.HasValue && !hasSearchAfter) root["from"] = spec.From.Value;
        if (spec.Size.HasValue) root["size"] = spec.Size.Value;
        if (!spec.IncludeDetails)
        {
            root["_source"] = new JObject { ["excludes"] = new JArray(_detailFields.Split(',')) };
        }
        // Sorts
        var sorts = new JArray();
        if (spec.Sorts != null && spec.Sorts.Count > 0)
        {
            foreach (var s in spec.Sorts)
            {
                if (!string.IsNullOrWhiteSpace(s.Script)) continue; // skip script for now
                var fld = NormalizeSortField(s.Field);
                sorts.Add(new JObject { [fld] = new JObject { ["order"] = (s.Order?.ToLower() == "desc" ? "desc" : "asc") } });
            }
        }
        else if (!string.IsNullOrWhiteSpace(spec.Sort))
        {
            var parts = spec.Sort.Contains(',') ? spec.Sort.Split(',') : spec.Sort.Split(':');
            if (parts.Length == 2)
            {
                var fld = NormalizeSortField(parts[0].Trim());
                var ord = parts[1].Trim().ToLower() == "desc" ? "desc" : "asc";
                sorts.Add(new JObject { [fld] = new JObject { ["order"] = ord } });
            }
        }
        // Always add _id tie-breaker
        sorts.Add(new JObject { ["_id"] = new JObject { ["order"] = "asc" } });
        root["sort"] = sorts;

        if (hasSearchAfter)
        {
            var sa = new JArray();
            foreach (var o in spec.SearchAfter) sa.Add(JToken.FromObject(o));
            root["search_after"] = sa;
            // When using search_after, ES requires that from is not used
            root.Remove("from");
        }

        if (usePit)
        {
            root["pit"] = new JObject { ["id"] = pitId, ["keep_alive"] = "2m" };
        }
        root["query"] = spec.RawQuery!; // exact pass-through

        if (debug)
        {
            Console.WriteLine($"[ES-RAW] {typeof(T).Name} {url}{path} body={root}");
        }

        using var http = new HttpClient();
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var bytes = Encoding.ASCII.GetBytes($"{username}:{password}");
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }
        var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url + path)
        {
            Content = new System.Net.Http.StringContent(root.ToString(), Encoding.UTF8, "application/json")
        };
        var resp = await http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"ES search failed: {(int)resp.StatusCode} {json}");
        }
        var parsed = JObject.Parse(json);
        var hitsToken = parsed["hits"]?["hits"] as JArray ?? new JArray();
        var total = parsed["hits"]?["total"]?[(object)"value"]?.Value<long?>() ?? parsed["hits"]?["total"]?.Value<long?>() ?? hitsToken.Count;
        var app = new AppSearchResponse<T> { Total = total, PointInTimeId = pitId, IsValid = true };
        foreach (var h in hitsToken)
        {
            var id = h["_id"]?.ToString() ?? string.Empty;
            var sortArr = (h["sort"] as JArray)?.Select(x => (object)(x.Type == JTokenType.Integer ? (long)x : x.Type == JTokenType.Float ? (double)x : (string)x)).ToList() ?? new List<object>();
            var src = h["_source"]?.ToString() ?? "{}";
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(src)!;
            try { ((CategorizedEntity<string>)(object)obj).Id = id; } catch {}
            app.Hits.Add(new AppHit<T> { Id = id, Source = obj, Sorts = sortArr });
        }
        return app;
    }

    private async Task<JObject> PostRawAsync(JObject body, string? pitId, bool useIndexPath)
    {
        var url = _configuration["Elasticsearch:Url"]?.TrimEnd('/') ?? "http://localhost:9200";
        var username = _configuration["Elasticsearch:Username"];
        var password = _configuration["Elasticsearch:Password"];
        var path = useIndexPath ? $"/{_indexName}/_search" : "/_search";
        if (!string.IsNullOrEmpty(pitId))
        {
            body["pit"] = new JObject { ["id"] = pitId, ["keep_alive"] = "2m" };
        }
        using var http = new HttpClient();
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var bytes = Encoding.ASCII.GetBytes($"{username}:{password}");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        }
        var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url + path)
        {
            Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
        };
        var resp = await http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new Exception($"ES raw post failed: {(int)resp.StatusCode} {json}");
        return JObject.Parse(json);
    }

    // Removed NEST legacy search after v8 migration


    /// <summary>
    /// Searches for ViewResults by performing an aggregation using the provided search request
    /// </summary>
    /// <param name="request">The aggregation request to execute</param>
    /// <returns>The search response containing ViewResults</returns>
    public async Task<List<ViewResultDto>> SearchUsingViewAsync(string query, ViewDto viewDto, int from, int size)
    {
        List<ViewResultDto> content = new();
        var queryObj = JObject.Parse(query);

        // distinct aggregation
        var root = new JObject
        {
            ["size"] = 0,
            ["query"] = queryObj,
            ["aggs"] = new JObject
            {
                ["distinct"] = string.IsNullOrEmpty(viewDto.Script)
                    ? new JObject { ["terms"] = new JObject { ["field"] = viewDto.Aggregation, ["size"] = 10000 } }
                    : new JObject { ["terms"] = new JObject { ["script"] = new JObject { ["source"] = viewDto.Script }, ["size"] = 10000 } }
            }
        };
        var distinct = await PostRawAsync(root, null, true);
        var buckets = (distinct["aggregations"]?["distinct"]?["buckets"] as JArray) ?? new JArray();
        foreach (var b in buckets)
        {
            string categoryName = b["key"]?.ToString() ?? b["key_as_string"]?.ToString() ?? string.Empty;
            bool notCategorized = false;
            if (Regex.IsMatch(categoryName, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}.\d{3}Z"))
            {
                categoryName = Regex.Replace(categoryName, @"(\d{4})-(\d{2})-(\d{2})T\d{2}:\d{2}:\d{2}.\d{3}Z", "$1-$2-$3");
            }
            if (string.IsNullOrEmpty(categoryName)) { categoryName = "(Uncategorized)"; notCategorized = true; }
            content.Add(new ViewResultDto { CategoryName = categoryName, Count = b["doc_count"]?.Value<long?>(), NotCategorized = notCategorized });
        }

        content = content.OrderBy(cat => cat.CategoryName).ToList();

        var baseField = (viewDto.Aggregation ?? string.Empty).Replace(".keyword", string.Empty);
        var uncRoot = new JObject
        {
            ["size"] = 0,
            ["query"] = queryObj,
            ["aggs"] = new JObject
            {
                ["uncategorized"] = new JObject
                {
                    ["filter"] = new JObject
                    {
                        ["bool"] = new JObject
                        {
                            ["must_not"] = new JArray { new JObject { ["exists"] = new JObject { ["field"] = baseField } } }
                        }
                    }
                }
            }
        };
        var unc = await PostRawAsync(uncRoot, null, true);
        long uncatCount = unc["aggregations"]?["uncategorized"]?["doc_count"]?.Value<long?>() ?? 0;
        if (uncatCount > 0)
        {
            var existingUncategorized = content.FirstOrDefault(c => c.NotCategorized == true);
            if (existingUncategorized != null) existingUncategorized.Count = (existingUncategorized.Count ?? 0) + uncatCount;
            else content.Add(new ViewResultDto { CategoryName = "(Uncategorized)", Selected = false, NotCategorized = true, Count = uncatCount });
        }
        return content;
    }

    // Legacy overload removed after v8 migration

    // ToCurl helper removed after v8 migration

    /// <summary>
    /// Indexes a new Entity document
    /// </summary>
    /// <param name="Entity">The Entity document to index</param>
    /// <returns>The index response</returns>
    public async Task<WriteResult> IndexAsync(T entity)
    {
        var resp = await _elasticClient.IndexAsync(entity, i => i.Index(_indexName).Refresh(Refresh.WaitFor));
        return new WriteResult { Success = resp.IsValidResponse, Message = resp.Result.ToString() };
    }

    /// <summary>
    /// Updates an existing Entity document
    /// </summary>
    /// <param name="id">The ID of the document to update</param>
    /// <param name="Entity">The updated Entity document</param>
    /// <returns>The update response</returns>
    public async Task<WriteResult> UpdateAsync(string id, T entity)
    {
        // Use index as upsert/replace for simplicity
        var resp = await _elasticClient.IndexAsync(entity, i => i.Index(_indexName).Id(id).Refresh(Refresh.WaitFor));
        return new WriteResult { Success = resp.IsValidResponse, Message = resp.Result.ToString() };
    }

    /// <summary>
    /// Deletes a entity document
    /// </summary>
    /// <param name="id">The ID of the document to delete</param>
    /// <returns>The delete response</returns>
    public async Task<WriteResult> DeleteAsync(string id)
    {
        var resp = await _elasticClient.DeleteAsync<T>(id, d => d.Index(_indexName));
        return new WriteResult { Success = resp.IsValidResponse, Message = resp.Result.ToString() };
    }

    /// <summary>
    /// Gets unique values for a field in the entity index
    /// </summary>
    /// <param name="field">The field to get unique values for</param>
    /// <returns>A collection of unique field values</returns>
    public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
    {
        var body = new JObject
        {
            ["size"] = 0,
            ["aggs"] = new JObject
            {
                ["distinct"] = new JObject
                {
                    ["terms"] = new JObject
                    {
                        ["field"] = field,
                        ["size"] = 10000
                    }
                }
            }
        };
        var json = await PostRawAsync(body, null, true);
        var arr = (json["aggregations"]?["distinct"]?["buckets"] as JArray) ?? new JArray();
        var ret = new List<string>();
        foreach (var b in arr)
        {
            var v = b["key"]?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(v)) ret.Add(v);
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
    public async Task<AppSearchResponse<T>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, string? sort = null)
    {
        var queryObject = await ConvertRulesetToElasticSearch(ruleset);
        var spec = new SearchSpec<T>
        {
            Size = size,
            From = from,
            RawQuery = queryObject,
            IncludeDetails = false,
            Sort = sort
        };
        return await SearchAsync(spec);
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

    public async Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto viewDto, int size = 20, int from = 0, string? sort = null)
    {
        string query = queryObject.ToString();        
        return await SearchUsingViewAsync(query, viewDto, from, size);
    }

    public async Task<SimpleApiResponse> CategorizeAsync(CategorizeRequestDto request)
    {
        var spec = new SearchSpec<T> { Ids = request.Ids, IncludeDetails = true, PitId = "" };
        var response = await SearchAsync(spec);
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
                            if (updateResponse.Success)
                            {
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                                errorMessages.Add($"Failed to update entity {entity.Id}: {updateResponse.Message}");
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
                        if (updateResponse.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errorMessages.Add($"Failed to update entity {entity.Id}: {updateResponse.Message}");
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

        var spec = new SearchSpec<T> { Ids = request.Rows, IncludeDetails = true, PitId = "" };
        var response = await SearchAsync(spec);
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
                if (updateResponse.Success)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    errorMessages.Add($"Failed to update entity {entity.Id}: {updateResponse.Message}");
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

    public async Task<ClusterHealthDto> GetHealthAsync()
    {
        var res = await _elasticClient.Cluster.HealthAsync();
        return new ClusterHealthDto
        {
            Status = res.Status.ToString(),
            NumberOfNodes = res.NumberOfNodes,
            NumberOfDataNodes = res.NumberOfDataNodes,
            ActiveShards = res.ActiveShards,
            ActivePrimaryShards = res.ActivePrimaryShards
        };
    }
}
