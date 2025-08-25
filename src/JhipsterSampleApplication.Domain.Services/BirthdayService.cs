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
    public async Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr)
    {
        var dto = ToDto(rr);
        var result = await _bqlService.Ruleset2ElasticSearch(dto);
        return result as JObject ?? new JObject();
    }

    private static RulesetDto ToDto(Ruleset rule)
    {
        return new RulesetDto
        {
            field = rule.field,
            @operator = rule.@operator,
            value = rule.value,
            condition = rule.condition,
            @not = rule.@not,
            rules = rule.rules?.Select(ToDto).ToList()
        };
    }
    private string ToElasticRegEx(string pattern, Boolean bCaseInsensitive)
    {
        string ret = "";
        string[] regexTokens = Regex.Replace(pattern, @"([\[\]]|\\\\|\\\[|\\\]|\\s|\\S|\\w|\\W|\\d|\\D|.)", "`$1").Split('`');
        Boolean bBracketed = false;
        for (int i = 1; i < regexTokens.Length; i++){
            if (bBracketed){
                switch (regexTokens[i]){
                    case "]":
                        bBracketed = false;
                        ret += regexTokens[i];
                        break;
                    case @"\s":
                        ret += " \n\t\r";
                        break;
                    case @"\d":
                        ret += "0-9";
                        break;
                    case @"\w":
                        ret += "A-Za-z_";
                        break;
                    default:
                        if (bCaseInsensitive && Regex.IsMatch(regexTokens[i], @"^[A-Za-z]+$")){
                            if ((i + 2) < regexTokens.Length && regexTokens[i + 1] == "-" && Regex.IsMatch(regexTokens[i + 2], @"^[A-Za-z]+$")){
                                // alpha rannge
                                ret += (regexTokens[i].ToLower() + "-" + regexTokens[i + 2].ToLower() + regexTokens[i].ToUpper() + "-" + regexTokens[i + 2].ToUpper());
                                i += 2;
                            } else {
                                ret += (regexTokens[i].ToLower() + regexTokens[i].ToUpper());
                            }
                        } else {
                            ret += regexTokens[i];
                        }
                        break;
                }
            } else if (regexTokens[i] == "["){
                bBracketed = true;
                ret += regexTokens[i];
            } else if (regexTokens[i] == @"\s"){
                ret += (@"[ \n\t\r]");
            } else if (regexTokens[i] == @"\d"){
                ret += (@"[0-9]");
            } else if (regexTokens[i] == @"\w"){
                ret += (@"[A-Za-z_]");
            } else if (bCaseInsensitive && Regex.IsMatch(regexTokens[i], @"[A-Za-z]")){
                ret += ("[" + regexTokens[i].ToLower() + regexTokens[i].ToUpper() + "]");
            } else {
                ret += regexTokens[i];
            }
        }
        return ret;
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
} 