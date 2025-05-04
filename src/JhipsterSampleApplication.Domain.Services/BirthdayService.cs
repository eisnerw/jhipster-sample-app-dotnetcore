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

namespace JhipsterSampleApplication.Domain.Services;

/// <summary>
/// Service for interacting with Elasticsearch for Birthday operations
/// </summary>
public class BirthdayService : IBirthdayService, IGenericElasticSearchService<Birthday>
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "birthdays";

    /// <summary>
    /// Initializes a new instance of the BirthdayService
    /// </summary>
    /// <param name="elasticClient">The Elasticsearch client</param>
    public BirthdayService(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
    }

    /// <summary>
    /// Searches for Birthday documents using the provided search request
    /// </summary>
    /// <param name="request">The search request to execute</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<Birthday>> SearchAsync(ISearchRequest request)
    {
        var response = await _elasticClient.SearchAsync<Birthday>(request);
        if (response.IsValid && response.Hits.Count > 0)
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
    /// Searches for Birthday documents using the provided search request in lucene format
    /// </summary>
    /// <param name="luceneQuery">The search request to execute</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<Birthday>> SearchWithLuceneQueryAsync(string luceneQuery)
    {
        var response = await _elasticClient.SearchAsync<Birthday>(s => s
            .Index("birthdays")
            .Query(q => q
                .QueryString(qs => qs
                    .Query(luceneQuery)
                )
            )
        );
        if (response.IsValid && response.Hits.Count > 0)
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
    public async Task<IReadOnlyCollection<string>> GetUniqueFieldValuesAsync(string field)
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
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<Birthday>> SearchWithRulesetAsync(RulesetOrRule ruleset, int size = 10000)
    {
        var queryObject = await ConvertRulesetToElasticSearch(ruleset);
        string query = queryObject.ToString();
        var searchRequest = new SearchRequest<Birthday>
        {
            Size = 10000,
            From = 0,
            Query = new QueryContainerDescriptor<Birthday>().Raw(query)
        };
        return await SearchAsync(searchRequest);     
    }

    /// <summary>
    /// Converts a ruleset to an Elasticsearch query
    /// </summary>
    /// <param name="rr">The ruleset to convert</param>
    /// <returns>A JObject containing the Elasticsearch query</returns>
    private async Task<JObject> ConvertRulesetToElasticSearch(RulesetOrRule rr)
    {
        if (rr.rules == null)
        {
            string stringValue = rr.value?.ToString() ?? string.Empty;
            if (rr.@operator != null && rr.@operator.Contains("contains"))
            {
                string regex = stringValue.ToLower().Replace(@"\", @"\\").Replace(@".", @"\.").Replace(@"*", @".*");
                if (rr.field == "document")
                {
                    List<JObject> lstRegexes = "wikipedia,fname,lname,categories,sign,id".Split(',').ToList().Select(s =>
                    {
                        return new JObject{
                            {
                                "regexp", new JObject{
                                    {
                                        s + ".keyword", new JObject{
                                            { "value", regex },
                                            { "flags", "ALL" },
                                            { "rewrite", "constant_score" }
                                        }
                                    }
                                }
                            }
                        };
                    }).ToList();
                    return new JObject{
                        {
                            "bool", new JObject{
                                { "should", JArray.FromObject(lstRegexes) }
                            }
                        }
                    };
                }
                return new JObject{
                    {
                        "regexp", new JObject{
                            {
                                rr.field + ".keyword", new JObject{
                                    { "value", regex },
                                    { "flags", "ALL" },
                                    { "rewrite", "constant_score" }
                                }
                            }
                        }
                    }
                };
            }
            else if (rr.@operator != null && rr.@operator.Contains("="))
            {
                List<string> uniqueValues = (await GetUniqueFieldValuesAsync(rr.field + ".keyword")).ToList();
                string valueToMatch = rr.value?.ToString() ?? string.Empty;
                List<JObject> oredTerms = uniqueValues.Where(v => v.ToLower() == valueToMatch.ToLower()).Select(s =>
                {
                    return new JObject{
                        {
                            "term", new JObject{
                                { rr.field + ".keyword", s }
                            }
                        }
                    };
                }).ToList();
                if (oredTerms.Count > 1)
                {
                    return new JObject{
                        {
                            "bool", new JObject{
                                { "should", JArray.FromObject(oredTerms) }
                            }
                        }
                    };
                }
                else if (oredTerms.Count == 1)
                {
                    return oredTerms[0];
                }
            }
            else if (rr.@operator != null && rr.@operator.Contains("in"))
            {
                List<string> uniqueValues = (await GetUniqueFieldValuesAsync(rr.field + ".keyword")).ToList();
                List<string> caseSensitiveMatches = new List<string>();
                if (rr.value is JArray jArray)
                {
                    caseSensitiveMatches = jArray.Select(v =>
                    {
                        string value = v?.ToString() ?? string.Empty;
                        return uniqueValues.Where(s => s.ToLower() == value.ToLower());
                    }).Aggregate((agg, list) => {
                        return agg.Concat(list).ToList();
                    }).ToList();
                }
                return new JObject{
                    {
                        "terms", new JObject{
                            { rr.field + ".keyword", JArray.FromObject(caseSensitiveMatches) }
                        }
                    }
                };
            }
        }
        return new JObject();
    }
} 