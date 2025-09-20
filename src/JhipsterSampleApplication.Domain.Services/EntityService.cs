using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using JhipsterSampleApplication.Dto;
using System.Threading;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Specialized;
using System.Net;
using Elastic.Transport;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using JhipsterSampleApplication.Domain.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using System.Globalization;

namespace JhipsterSampleApplication.Domain.Services;

/// <summary>
/// Service for interacting with Elasticsearch for Entity operations
/// </summary>
public class EntityService : IEntityService
{
    private readonly ElasticsearchClient _elasticClient;
    // Verbose ES request logging (can be toggled via env var ES_DEBUG=true)
    private static bool debug = Environment.GetEnvironmentVariable("ES_DEBUG")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    private readonly IEntitySpecRegistry _specRegistry;
    private readonly INamedQueryService _namedQueryService;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the EntityService
    /// </summary>
    /// <param name="elasticClient">The Elasticsearch client</param>
    /// <param name="bqlService">The BQL service</param>
    /// <param name="viewService">The View service</param>
    public EntityService(IServiceProvider serviceProvider, INamedQueryService namedQueryService, IEntitySpecRegistry specRegistry)
    {
        _elasticClient = serviceProvider.GetRequiredService<ElasticsearchClient>();
        _specRegistry = specRegistry ?? throw new ArgumentNullException(nameof(specRegistry));
        _namedQueryService = namedQueryService ?? throw new ArgumentNullException(nameof(namedQueryService));
        _loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? LoggerFactory.Create(_ => { });
    }

    private (string Index, string IdField) GetEntitySpec(string entity)
    {
        // Look up the Elasticsearch index for the entity.  If it is not present we
        // treat the entity as unknown.
        if (!_specRegistry.TryGetString(entity, "elasticsearchIndex", out var index)
            && !_specRegistry.TryGetString(entity, "elasticSearchIndex", out index)
            && !_specRegistry.TryGetString(entity, "index", out index))
            throw new ArgumentException($"Unknown entity '{entity}'", nameof(entity));

        var idField = "Id";
        if (_specRegistry.TryGetString(entity, "idField", out var id) && !string.IsNullOrWhiteSpace(id))
            idField = id;

        return (index, idField);
    }

    private string NormalizeSortField(string entity, string field)
    {
        if (string.Equals(field, "_id", StringComparison.OrdinalIgnoreCase)) return "_id";
        if (string.Equals(field, "id", StringComparison.OrdinalIgnoreCase)) return "_id";
        if (!field.Contains(".keyword")){
            field = $"{field}.keyword";
        }
        return field;
    }

    private static string ToCamel(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.Length == 1) return name.ToLowerInvariant();
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static JToken ToCamelCaseKeys(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                var obj = (JObject)token;
                var newObj = new JObject();
                foreach (var p in obj.Properties())
                {
                    var newName = ToCamel(p.Name);
                    newObj[newName] = ToCamelCaseKeys(p.Value);
                }
                return newObj;
            case JTokenType.Array:
                var arr = new JArray();
                foreach (var item in (JArray)token) arr.Add(ToCamelCaseKeys(item));
                return arr;
            default:
                return token.DeepClone();
        }
    }

    private static object? JTokenToPlain(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                var obj = (JObject)token;
                var dict = new Dictionary<string, object?>();
                foreach (var p in obj.Properties())
                    dict[p.Name] = JTokenToPlain(p.Value);
                return dict;
            case JTokenType.Array:
                var list = new List<object?>();
                foreach (var item in (JArray)token) list.Add(JTokenToPlain(item));
                return list;
            case JTokenType.Integer:
                return token.Value<long>();
            case JTokenType.Float:
                return token.Value<double>();
            case JTokenType.Boolean:
                return token.Value<bool>();
            case JTokenType.Date:
                // Preserve ISO-8601 for Elasticsearch date mapping compatibility
                var dt = token.Value<DateTime>();
                if (dt.Kind == DateTimeKind.Unspecified)
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return dt.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            case JTokenType.String:
                return token.Value<string>();
            case JTokenType.Null:
            case JTokenType.Undefined:
                return null;
            default:
                return token.ToString();
        }
    }

    public async Task<AppSearchResponse<JObject>> SearchAsync(string entity, SearchSpec<JObject> spec)
    {
        return await SearchRawAsync(entity, spec);
    }

    private static JToken ConvertObjectToJToken(object? value)
    {
        if (value == null) return JValue.CreateNull();
        if (value is JToken jt) return jt;
        if (value is JsonElement je) return ConvertJsonElement(je);
        if (value is IDictionary<string, object> dict)
        {
            var o = new JObject();
            foreach (var kv in dict)
            {
                o[kv.Key] = ConvertObjectToJToken(kv.Value);
            }
            return o;
        }
        if (value is IEnumerable<object> list)
        {
            var arr = new JArray();
            foreach (var item in list) arr.Add(ConvertObjectToJToken(item));
            return arr;
        }
        return JToken.FromObject(value);
    }

    private static JToken ConvertJsonElement(JsonElement je)
    {
        switch (je.ValueKind)
        {
            case JsonValueKind.Object:
                var o = new JObject();
                foreach (var p in je.EnumerateObject())
                {
                    o[p.Name] = ConvertJsonElement(p.Value);
                }
                return o;
            case JsonValueKind.Array:
                var a = new JArray();
                foreach (var el in je.EnumerateArray()) a.Add(ConvertJsonElement(el));
                return a;
            case JsonValueKind.String:
                return new JValue(je.GetString());
            case JsonValueKind.Number:
                if (je.TryGetInt64(out var l)) return new JValue(l);
                if (je.TryGetDouble(out var d)) return new JValue(d);
                return new JValue(je.GetRawText());
            case JsonValueKind.True:
                return new JValue(true);
            case JsonValueKind.False:
                return new JValue(false);
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return JValue.CreateNull();
            default:
                return new JValue(je.GetRawText());
        }
    }

    private async Task<AppSearchResponse<JObject>> SearchRawAsync(string entity, SearchSpec<JObject> spec)
    {
        var (indexName, _) = GetEntitySpec(entity);

        // Fast path for single-id lookup: do a direct GET and avoid PIT/search
        if (!string.IsNullOrWhiteSpace(spec.Id) && (spec.Ids == null || spec.Ids.Count == 0) && spec.RawQuery == null)
        {
            var get = await _elasticClient.GetAsync<Dictionary<string, object>>(spec.Id!, g => g.Index(indexName));
            var appDirect = new AppSearchResponse<JObject> { Total = get.Found ? 1 : 0, PointInTimeId = null, IsValid = get.IsValidResponse };
            if (get.Found && get.Source != null)
            {
                var src = ConvertObjectToJToken(get.Source) as JObject ?? new JObject();
                if (src["id"] == null) src["id"] = get.Id;
                appDirect.Hits.Add(new AppHit<JObject> { Id = get.Id, Source = src, Sorts = new List<object>() });
            }
            return appDirect;
        }

        // Ensure we have a PIT for the first page to anchor paging across updates/deletes
        string? pitId = spec.PitId;
        if (string.IsNullOrWhiteSpace(pitId))
        {
            try
            {
                var pitResponse = await _elasticClient.OpenPointInTimeAsync(new OpenPointInTimeRequest(indexName)
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
        var path = usePit ? "/_search" : $"/{indexName}/_search";

        var root = new JObject();
        var hasSearchAfter = spec.SearchAfter != null && spec.SearchAfter.Count > 0;
        if (spec.From.HasValue && !hasSearchAfter) root["from"] = spec.From.Value;
        if (spec.Size.HasValue) root["size"] = spec.Size.Value;
        if (!spec.IncludeDetails)
        {
            // Restrict returned fields strictly to those listed in the entity's
            // "columns" (or legacy "listFields"). No name normalization.
            var includesSet = new HashSet<string>();
            System.Text.Json.Nodes.JsonArray colsArr;
            if (_specRegistry.TryGetArray(entity, "columns", out colsArr) || _specRegistry.TryGetArray(entity, "listFields", out colsArr))
            {
                foreach (var node in colsArr)
                {
                    try
                    {
                        if (node is System.Text.Json.Nodes.JsonValue jv)
                        {
                            var s = jv.GetValue<string?>();
                            if (!string.IsNullOrWhiteSpace(s)) includesSet.Add(s!);
                        }
                        else if (node is System.Text.Json.Nodes.JsonObject jo)
                        {
                            var s = jo["field"]?.GetValue<string?>();
                            if (!string.IsNullOrWhiteSpace(s)) includesSet.Add(s!);
                        }
                    }
                    catch { /* ignore malformed */ }
                }
            }

            if (includesSet.Count > 0)
            {
                var includes = new JArray();
                foreach (var key in includesSet) includes.Add(key);
                root["_source"] = new JObject { ["includes"] = includes };
            }
        }
        // Sorts
        var sorts = new JArray();
        bool hasExplicitIdSort = false;
        if (spec.Sorts != null && spec.Sorts.Count > 0)
        {
            foreach (var s in spec.Sorts)
            {
                if (!string.IsNullOrWhiteSpace(s.Script)) continue; // skip script for now
                var fld = NormalizeSortField(entity, s.Field);
                if (string.Equals(fld, "_id", StringComparison.OrdinalIgnoreCase)) hasExplicitIdSort = true;
                sorts.Add(new JObject { [fld] = new JObject { ["order"] = (s.Order?.ToLower() == "desc" ? "desc" : "asc") } });
            }
        }
        else if (!string.IsNullOrWhiteSpace(spec.Sort))
        {
            var parts = spec.Sort.Contains(',') ? spec.Sort.Split(',') : spec.Sort.Split(':');
            if (parts.Length == 2)
            {
                var fld = NormalizeSortField(entity, parts[0].Trim());
                var ord = parts[1].Trim().ToLower() == "desc" ? "desc" : "asc";
                if (string.Equals(fld, "_id", StringComparison.OrdinalIgnoreCase)) hasExplicitIdSort = true;
                sorts.Add(new JObject { [fld] = new JObject { ["order"] = ord } });
            }
        }
        // Always add _id tie-breaker if not already explicitly sorted by _id
        if (!hasExplicitIdSort)
        {
            sorts.Add(new JObject { ["_id"] = new JObject { ["order"] = "asc" } });
        }
        root["sort"] = sorts;

        if (hasSearchAfter)
        {
            var sa = new JArray();
            foreach (var o in spec.SearchAfter!) sa.Add(JToken.FromObject(o));
            root["search_after"] = sa;
            // When using search_after, ES requires that from is not used
            root.Remove("from");
        }

        if (usePit)
        {
            root["pit"] = new JObject { ["id"] = pitId, ["keep_alive"] = "2m" };
        }
        // Build query
        if (!string.IsNullOrWhiteSpace(spec.Id))
        {
            root["query"] = new JObject { ["ids"] = new JObject { ["values"] = new JArray(spec.Id) } };
        }
        else if (spec.Ids != null && spec.Ids.Count > 0)
        {
            root["query"] = new JObject { ["ids"] = new JObject { ["values"] = new JArray(spec.Ids) } };
        }
        else
        {
            root["query"] = spec.RawQuery ?? new JObject { ["match_all"] = new JObject() };
        }

        if (debug)
        {
            Console.WriteLine($"[ES-RAW] entity='{entity}' {path} body={root}");
        }

        var endpoint = new EndpointPath(HttpMethod.POST, path);
        var esResp = await _elasticClient.Transport.RequestAsync<StringResponse>(in endpoint, PostData.String(root.ToString()), null, null, CancellationToken.None);
        if (!(esResp.ApiCallDetails?.HasSuccessfulStatusCode ?? false))
        {
            var status = esResp.ApiCallDetails?.HttpStatusCode ?? 0;
            var body = esResp.ApiCallDetails?.ResponseBodyInBytes != null ? Encoding.UTF8.GetString(esResp.ApiCallDetails.ResponseBodyInBytes) : esResp.Body;
            throw new Exception($"ES search failed: {status} {body}");
        }
        var json = esResp.Body ?? string.Empty;
        if (debug)
        {
            Console.WriteLine($"[ES-RESP] status={(esResp.ApiCallDetails?.HttpStatusCode ?? 0)} body={json}");
        }
        var parsed = JObject.Parse(json);
        var hitsToken = parsed["hits"]?["hits"] as JArray ?? new JArray();
        var total = parsed["hits"]?["total"]?[(object)"value"]?.Value<long?>() ?? parsed["hits"]?["total"]?.Value<long?>() ?? hitsToken.Count;
        var app = new AppSearchResponse<JObject> { Total = total, PointInTimeId = pitId, IsValid = true };
        foreach (var h in hitsToken)
        {
            var id = h["_id"]?.ToString() ?? string.Empty;
            var sortsToken = h["sort"] as JArray;
            var sortsList = new List<object>();
            if (sortsToken != null)
            {
                foreach (var x in sortsToken)
                {
                    object? val;
                    if (x.Type == JTokenType.Integer) val = (long)x;
                    else if (x.Type == JTokenType.Float) val = (double)x;
                    else if (x.Type == JTokenType.Null || x.Type == JTokenType.Undefined) val = null;
                    else val = x.Value<string>() ?? x.ToString();
                    if (val != null) sortsList.Add(val);
                }
            }
            var obj = (h["_source"] as JObject) ?? new JObject();
            if (obj["id"] == null) obj["id"] = id; // lowercase id for clients
            app.Hits.Add(new AppHit<JObject> { Id = id, Source = obj, Sorts = sortsList });
        }
        return app;
    }

    private async Task<JObject> PostRawAsync(string indexName, JObject body, string? pitId, bool useIndexPath)
    {
        var path = useIndexPath ? $"/{indexName}/_search" : "/_search";
        if (!string.IsNullOrEmpty(pitId))
        {
            body["pit"] = new JObject { ["id"] = pitId, ["keep_alive"] = "2m" };
        }
        var endpoint = new EndpointPath(HttpMethod.POST, path);
        var esResp = await _elasticClient.Transport.RequestAsync<StringResponse>(in endpoint, PostData.String(body.ToString()), null, null, CancellationToken.None);
        if (!(esResp.ApiCallDetails?.HasSuccessfulStatusCode ?? false))
        {
            var status = esResp.ApiCallDetails?.HttpStatusCode ?? 0;
            var respBody = esResp.ApiCallDetails?.ResponseBodyInBytes != null ? Encoding.UTF8.GetString(esResp.ApiCallDetails.ResponseBodyInBytes) : esResp.Body;
            throw new Exception($"ES raw post failed: {status} {respBody}");
        }
        return JObject.Parse(esResp.Body ?? "{}");
    }

    // Removed NEST legacy search after v8 migration


    /// <summary>
    /// Searches for ViewResults by performing an aggregation using the provided search request
    /// </summary>
    /// <param name="request">The aggregation request to execute</param>
    /// <returns>The search response containing ViewResults</returns>
    private async Task<List<ViewResultDto>> SearchUsingViewAsync(string indexName, string query, ViewDto viewDto, int from, int size)
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
        var distinct = await PostRawAsync(indexName, root, null, true);
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
        var unc = await PostRawAsync(indexName, uncRoot, null, true);
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

    public async Task<WriteResult> IndexAsync(string entity, JObject document)
    {
        var (indexName, idField) = GetEntitySpec(entity);
        var id = document[idField]?.ToString() ?? document["Id"]?.ToString() ?? document["id"]?.ToString();
        var camelDoc = (JObject)ToCamelCaseKeys(document);
        Elastic.Clients.Elasticsearch.IndexResponse resp;
        if (!string.IsNullOrWhiteSpace(id))
        {
            var payload = JTokenToPlain(camelDoc)!;
            resp = await _elasticClient.IndexAsync<object>(payload, i => i.Index(indexName).Id(id!).Refresh(Refresh.WaitFor));
        }
        else
        {
            var payload = JTokenToPlain(camelDoc)!;
            resp = await _elasticClient.IndexAsync<object>(payload, i => i.Index(indexName).Refresh(Refresh.WaitFor));
        }
        if (debug)
        {
            Console.WriteLine($"[ES-INDEX] index={indexName} id={(id ?? "<auto>")} result={resp.Result} valid={resp.IsValidResponse}");
        }
        return new WriteResult { Success = resp.IsValidResponse, Message = resp.Result.ToString() };
    }

    public async Task<WriteResult> UpdateAsync(string entity, string id, JObject document)
    {
        var (indexName, _) = GetEntitySpec(entity);
        var camelDoc = (JObject)ToCamelCaseKeys(document);
        var payload = JTokenToPlain(camelDoc)!;
        var resp = await _elasticClient.IndexAsync<object>(payload, i => i.Index(indexName).Id(id).Refresh(Refresh.WaitFor));
        return new WriteResult { Success = resp.IsValidResponse, Message = resp.Result.ToString() };
    }

    public async Task<WriteResult> DeleteAsync(string entity, string id)
    {
        var (indexName, _) = GetEntitySpec(entity);
        var resp = await _elasticClient.DeleteAsync<object>(id, d => d.Index(indexName));
        return new WriteResult { Success = resp.IsValidResponse, Message = resp.Result.ToString() };
    }

    /// <summary>
    /// Gets unique values for a field in the entity index
    /// </summary>
    /// <param name="field">The field to get unique values for</param>
    /// <returns>A collection of unique field values</returns>
    public async Task<List<string>> GetUniqueFieldValuesAsync(string entity, string field)
    {
        var (indexName, _) = GetEntitySpec(entity);
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
        var json = await PostRawAsync(indexName, body, null, true);
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
    // Removed legacy generic SearchWithRulesetAsync after JSON refactor

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
public async Task<JObject> ConvertRulesetToElasticSearch(string entity, Ruleset rr)
{
    if (!ValidateRuleset(rr))
    {
        throw new ArgumentException("Invalid ruleset", nameof(rr));
    }

    var dto = MapToDto(rr);
    // Build BQL service for this entity on the fly using its QB spec
    JObject qbSpec;
    if (_specRegistry.TryGetObject(entity, "queryBuilder", out var qbNode))
    {
        qbSpec = JObject.Parse(qbNode.ToJsonString());
    }
    else
    {
        // Fallback 1: parse from Entities JSON in Resources if available
        var entitiesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Entities", $"{entity}.json");
        if (System.IO.File.Exists(entitiesPath))
        {
            var root = JObject.Parse(System.IO.File.ReadAllText(entitiesPath));
            qbSpec = (root["queryBuilder"] as JObject) ?? new JObject();
        }
        else
        {
            // Fallback 2: legacy query-builder file location
            qbSpec = BqlService<object>.LoadSpec(entity);
        }
    }
    // Ensure "document" field exists for BQL processing
    EnsureDocumentField(qbSpec);
    var logger = _loggerFactory.CreateLogger<BqlService<object>>();
    var bql = new BqlService<object>(logger, _namedQueryService, qbSpec, entity);
    var result = await bql.Ruleset2ElasticSearch(dto);
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

    public async Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(string entity, JObject queryObject, ViewDto viewDto, int size = 20, int from = 0, string? sort = null)
    {
        var (indexName, _) = GetEntitySpec(entity);
        string query = queryObject.ToString();
        return await SearchUsingViewAsync(indexName, query, viewDto, from, size);
    }

    public async Task<SimpleApiResponse> CategorizeAsync(string entity, CategorizeRequestDto request)
    {
        var (indexName, _) = GetEntitySpec(entity);
        var spec = new SearchSpec<JObject> { Ids = request.Ids, IncludeDetails = true, PitId = "" };
        var response = await SearchAsync(entity, spec);
        if (!response.IsValid)
        {
            return new SimpleApiResponse { Success = false, Message = "Failed to search for the entities" };
        }
        var successCount = 0;
        var errorCount = 0;
        var errorMessages = new List<string>();

        foreach (var hit in response.Hits)
        {
            try
            {
                var doc = hit.Source;
                var categories = (doc["categories"] as JArray) ?? new JArray();
                if (request.RemoveCategory)
                {
                    if (categories.Count > 0)
                    {
                        var toRemove = categories
                            .Select(t => t?.ToString() ?? string.Empty)
                            .FirstOrDefault(c => string.Equals(c, request.Category, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(toRemove))
                        {
                            var remaining = new JArray(categories.Where(t => !string.Equals(t?.ToString(), toRemove, StringComparison.OrdinalIgnoreCase)));
                            doc["categories"] = remaining;
                            var updateResponse = await UpdateAsync(entity, hit.Id, doc);
                            if (updateResponse.Success)
                            {
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                                errorMessages.Add($"Failed to update entity {hit.Id}: {updateResponse.Message}");
                            }
                        }
                    }
                }
                else
                {
                    var exists = categories.Any(t => string.Equals(t?.ToString(), request.Category, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        categories.Add(request.Category);
                        doc["categories"] = categories;
                        var updateResponse = await UpdateAsync(entity, hit.Id, doc);
                        if (updateResponse.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errorMessages.Add($"Failed to update entity {hit.Id}: {updateResponse.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                errorMessages.Add($"Error processing entity {hit.Id}: {ex.Message}");
            }
        }

        var message = $"Processed {request.Ids.Count} entities. Success: {successCount}, Errors: {errorCount}";
        if (errorMessages.Any())
        {
            message += $". Error details: {string.Join("; ", errorMessages)}";
        }
        await _elasticClient.Indices.RefreshAsync(indexName);
        return new SimpleApiResponse
        {
            Success = errorCount == 0,
            Message = message
        };
    }

    public async Task<SimpleApiResponse> CategorizeMultipleAsync(string entity, CategorizeMultipleRequestDto request)
    {
        var (indexName, _) = GetEntitySpec(entity);
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

        var spec = new SearchSpec<JObject> { Ids = request.Rows, IncludeDetails = true, PitId = "" };
        var response = await SearchAsync(entity, spec);
        if (!response.IsValid)
        {
            return new SimpleApiResponse { Success = false, Message = "Failed to search for Entitys" };
        }

        var successCount = 0;
        var errorCount = 0;
        var errorMessages = new List<string>();

        foreach (var hit in response.Hits)
        {
            try
            {
                var doc = hit.Source;
                var current = (doc["categories"] as JArray) ?? new JArray();
                if (toRemove.Any() && current.Any())
                {
                    current = new JArray(current.Where(t => !toRemove.Any(r => string.Equals(t?.ToString() ?? string.Empty, r, StringComparison.OrdinalIgnoreCase))));
                }

                foreach (var add in toAdd)
                {
                    if (!current.Any(t => string.Equals(t?.ToString() ?? string.Empty, add, StringComparison.OrdinalIgnoreCase)))
                    {
                        current.Add(add);
                    }
                }

                doc["categories"] = current;
                var updateResponse = await UpdateAsync(entity, hit.Id, doc);
                if (updateResponse.Success)
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                    errorMessages.Add($"Failed to update entity {hit.Id}: {updateResponse.Message}");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                errorMessages.Add($"Error processing entity {hit.Id}: {ex.Message}");
            }
        }

        var message = $"Processed {request.Rows.Count} entities. Success: {successCount}, Errors: {errorCount}";
        if (errorMessages.Any())
        {
            message += $". Error details: {string.Join("; ", errorMessages)}";
        }

        await _elasticClient.Indices.RefreshAsync(indexName);
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

    public static string InterpolateTemplate(string[] arTemplate, Newtonsoft.Json.Linq.JObject entityValues)
    {
        var sb = new StringBuilder();
        string currentTemplate = string.Empty;

        try
        {
            for (int i = 0; i < arTemplate.Length; i++)
            {
                currentTemplate = arTemplate[i] ?? string.Empty;
                var lineOut = new StringBuilder();
                var s = currentTemplate;

                int p = 0;
                while (p < s.Length)
                {
                    // Escaped opening brace "{{" => literal "{"
                    if (s[p] == '{' && p + 1 < s.Length && s[p + 1] == '{')
                    {
                        lineOut.Append('{');
                        p += 2;
                        continue;
                    }
                    // Escaped closing brace "}}" => literal "}"
                    if (s[p] == '}' && p + 1 < s.Length && s[p + 1] == '}')
                    {
                        lineOut.Append('}');
                        p += 2;
                        continue;
                    }

                    // Token start
                    if (s[p] == '{')
                    {
                        int end = s.IndexOf('}', p + 1);
                        if (end < 0)
                        {
                            // No matching }, treat as literal
                            lineOut.Append(s[p]);
                            p++;
                            continue;
                        }

                        string token = s.Substring(p + 1, end - (p + 1));

                        // Parse token: name[:formatter[:formatter...]]
                        string attrName;
                        var formatters = new List<string>();
                        var parts = token.Split(':');
                        attrName = parts.Length > 0 ? parts[0] : string.Empty;
                        if (parts.Length > 1)
                        {
                            for (int k = 1; k < parts.Length; k++) formatters.Add(parts[k]);
                        }

                        // Defaults
                        bool unlabeled = false;
                        bool yesno = false;
                        bool link = false;
                        bool html = false;
                        bool ifDifferent = false;
                        string? missing = null;
                        string? labelOverride = null;
                        string? fmt = null; // standard .NET format string
                        string? diffAttribute = null;

                        foreach (var f in formatters)
                        {
                            if (string.Equals(f, "unlabeled", StringComparison.OrdinalIgnoreCase)) { unlabeled = true; continue; }
                            if (string.Equals(f, "yesno", StringComparison.OrdinalIgnoreCase)) { yesno = true; continue; }
                            if (string.Equals(f, "link", StringComparison.OrdinalIgnoreCase)) { link = true; continue; }
                            if (string.Equals(f, "html", StringComparison.OrdinalIgnoreCase)) { html = true; continue; }

                            int dash = f.IndexOf('-');
                            if (dash > 0)
                            {
                                var kind = f.Substring(0, dash);
                                var val = f.Substring(dash + 1);
                                if (f.StartsWith("ifDifferentFrom-"))
                                {
                                    ifDifferent = true;
                                    diffAttribute = val;
                                }
                                else if (kind.Equals("missing", StringComparison.OrdinalIgnoreCase)) missing = val;
                                else if (kind.Equals("label", StringComparison.OrdinalIgnoreCase)) labelOverride = val;
                                else if (kind.Equals("format", StringComparison.OrdinalIgnoreCase)) fmt = val;
                                else if (kind.Equals("ifDifferentFrom, StringComparison.OrdinalIgnoreCase")) diffAttribute = val;
                            }
                        }

                        // Lookup value from JObject (exact key)
                        JToken? v = entityValues[attrName];
                        if (ifDifferent){
                            JToken? diffToken = entityValues[diffAttribute ?? ""];
                            if (diffToken != null && v != null && v.Type == JTokenType.String && diffToken.Type == JTokenType.String && diffToken.ToString().StartsWith(v.ToString()))
                            {
                                v = null;
                            }
                        }
                        // Convert to string according to rules
                        string? valueString = ConvertValueToString(v, yesno, fmt, html);

                        bool isMissing = string.IsNullOrEmpty(valueString?.Trim());
                        if (isMissing && missing != null)
                        {
                            valueString = missing;
                            isMissing = false; // treat as present for label rendering
                        }

                        string replacement = string.Empty;
                        if (!isMissing)
                        {
                            if (link)
                            {
                                string anchorText = labelOverride ?? attrName;
                                string href = html ? (valueString ?? string.Empty) : WebUtility.HtmlEncode(valueString ?? string.Empty);
                                string anchorTextEsc = html ? anchorText : WebUtility.HtmlEncode(anchorText);
                                replacement = $"<a href=\"{href}\">{anchorTextEsc}</a>";
                            }
                            else if (unlabeled)
                            {
                                replacement = valueString ?? string.Empty;
                            }
                            else
                            {
                                string labelText = (labelOverride ?? attrName.Substring(0,1).ToUpper() +  attrName.Substring(1).Replace('_', ' '));
                                replacement = $"<b>{labelText}:</b> {valueString}";
                            }

                            // Default trailing space unless token ends the line
                            bool atEnd = (end == s.Length - 1);
                            if (!atEnd && !unlabeled) replacement += " ";
                        }
                        // else empty replacement when missing and no :missing- formatter

                        lineOut.Append(replacement);
                        p = end + 1;
                        continue;
                    }

                    // Regular character
                    lineOut.Append(s[p]);
                    p++;
                }

                // Append two <br> after each template line except the last
                string lineToBeAppended = lineOut.ToString();
                if (lineToBeAppended.Trim().Length > 0)
                {
                    sb.Append(lineToBeAppended);
                    if (i < arTemplate.Length - 1)
                    {
                        sb.Append("<br><br>");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return $"Error: {WebUtility.HtmlEncode(ex.Message)} while processing template: {WebUtility.HtmlEncode(currentTemplate)}";
        }

        return sb.ToString();
    }

    private static string? ConvertValueToString(JToken? v, bool yesno, string? fmt, bool html)
    {
        if (v == null || v.Type == JTokenType.Null || v.Type == JTokenType.Undefined) return null;

        // Arrays: join by ", "
        if (v.Type == JTokenType.Array)
        {
            var items = new List<string>();
            foreach (var t in (JArray)v)
            {
                var s = ConvertValueToString(t, yesno, fmt, html);
                if (!string.IsNullOrEmpty(s)) items.Add(s!);
            }
            var joined = string.Join(", ", items);
            return html ? joined : WebUtility.HtmlEncode(joined);
        }

        // Booleans (with optional yes/no)
        if (v.Type == JTokenType.Boolean)
        {
            bool b = v.Value<bool>();
            var text = yesno ? (b ? "yes" : "no") : (b ? "true" : "false");
            return text; // booleans are plain text; encode later only when embedding labels
        }

        // Numbers (apply format if provided)
        if (v.Type == JTokenType.Integer || v.Type == JTokenType.Float)
        {
            IFormattable num;
            if (v.Type == JTokenType.Integer) num = (IFormattable)v.Value<long>();
            else num = (IFormattable)v.Value<double>();
            var txt = fmt == null ? num.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                                  : num.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
            return txt;
        } 
        // Strings and others
        var raw = v.Type == JTokenType.String ? (v.Value<string>() ?? string.Empty) : v.ToString();
        var reDate = new Regex(@"^\d{4}-\d{2}-\d{2}(?:[T\s]\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+\-]\d{2}:\d{2})?)?$");
        if (fmt != null &&reDate.IsMatch(raw) && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt.ToString(fmt, CultureInfo.InvariantCulture);
        }
        return html ? raw : WebUtility.HtmlEncode(raw);
    }
    public static void EnsureDocumentField(Newtonsoft.Json.Linq.JObject qbSpec)
    {
        if (qbSpec == null) return;
        var fields = qbSpec["fields"] as JObject;
        if (fields == null)
        {
            fields = new JObject();
            qbSpec["fields"] = fields;
        }
        if (fields.Property("document") == null)
        {
            var newFields = new JObject
            {
                ["document"] = new JObject
                {
                    ["name"] = "Document",
                    ["type"] = "string",
                    ["operators"] = new JArray("contains", "!contains", "like", "!like")
                }
            };
            foreach (var p in fields.Properties()) newFields[p.Name] = p.Value;
            qbSpec["fields"] = newFields;
        }
    }
}
