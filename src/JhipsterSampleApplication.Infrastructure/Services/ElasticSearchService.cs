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

namespace JhipsterSampleApplication.Infrastructure.Services;

/// <summary>
/// Service for interacting with Elasticsearch
/// </summary>
public class ElasticSearchService : IElasticSearchService, IGenericElasticSearchService
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "birthdays";

    /// <summary>
    /// Initializes a new instance of the ElasticSearchService
    /// </summary>
    /// <param name="configuration">The configuration containing Elasticsearch settings</param>
    /// <exception cref="ArgumentNullException">Thrown when ElasticSearch:Url is not configured</exception>
    public ElasticSearchService(IConfiguration configuration)
    {
        var url = configuration["ElasticSearch:Url"] ?? throw new ArgumentNullException("ElasticSearch:Url");
        var node = new Uri(url);
        var settings = new ConnectionSettings(node)
            .BasicAuthentication(
                configuration["ElasticSearch:Username"],
                configuration["ElasticSearch:Password"])
            .DefaultIndex(IndexName);

        _elasticClient = new ElasticClient(settings);
    }

    /// <summary>
    /// Searches for Birthday documents using the provided search request in json format
    /// </summary>
    /// <param name="request">The search request to execute</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<Birthday>> SearchAsync(ISearchRequest request)
    {
        var response = await _elasticClient.SearchAsync<Birthday>(request);

        // Map Elasticsearch document IDs to Birthday.Id properties
        if (response.IsValid && response.Hits.Count > 0)
        {
            foreach (var hit in response.Hits)
            {
                if (hit.Source != null)
                {
                    // Set the Birthday.Id to the Elasticsearch document ID
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
        // Map Elasticsearch document IDs to Birthday.Id properties
        if (response.IsValid && response.Hits.Count > 0)
        {
            foreach (var hit in response.Hits)
            {
                if (hit.Source != null)
                {
                    // Set the Birthday.Id to the Elasticsearch document ID
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
    /// Deletes a Birthday document by ID
    /// </summary>
    /// <param name="id">The ID of the Birthday document to delete</param>
    /// <returns>The delete response</returns>
    public async Task<DeleteResponse> DeleteAsync(string id)
    {
        return await _elasticClient.DeleteAsync<Birthday>(id);
    }

    /// <summary>
    /// Updates an existing Birthday document
    /// </summary>
    /// <param name="id">The ID of the Birthday document to update</param>
    /// <param name="birthday">The updated Birthday document</param>
    /// <returns>The update response</returns>
    public async Task<UpdateResponse<Birthday>> UpdateAsync(string id, Birthday birthday)
    {
        return await _elasticClient.UpdateAsync<Birthday>(id, u => u.Doc(birthday));
    }

    /// <summary>
    /// Searches for documents of type T using the provided search request
    /// </summary>
    /// <typeparam name="T">The type of document to search for</typeparam>
    /// <param name="searchRequest">The search request to execute</param>
    /// <returns>The search response containing documents of type T</returns>
    public async Task<ISearchResponse<T>> SearchAsync<T>(ISearchRequest searchRequest) where T : class
    {
        return await _elasticClient.SearchAsync<T>(searchRequest);
    }

    /// <summary>
    /// Indexes a new document of type T
    /// </summary>
    /// <typeparam name="T">The type of document to index</typeparam>
    /// <param name="document">The document to index</param>
    /// <param name="indexName">The name of the index to use</param>
    /// <returns>The index response</returns>
    public async Task<IndexResponse> IndexAsync<T>(T document, string indexName) where T : class
    {
        return await _elasticClient.IndexDocumentAsync(document);
    }

    /// <summary>
    /// Deletes a document of type T by ID
    /// </summary>
    /// <typeparam name="T">The type of document to delete</typeparam>
    /// <param name="id">The ID of the document to delete</param>
    /// <param name="indexName">The name of the index to use</param>
    /// <returns>The delete response</returns>
    public async Task<DeleteResponse> DeleteAsync<T>(string id, string indexName) where T : class
    {
        return await _elasticClient.DeleteAsync<T>(id);
    }

    /// <summary>
    /// Gets a document of type T by ID
    /// </summary>
    /// <typeparam name="T">The type of document to get</typeparam>
    /// <param name="id">The ID of the document to get</param>
    /// <param name="indexName">The name of the index to use</param>
    /// <returns>The get response containing the document</returns>
    public async Task<IGetResponse<T>> GetAsync<T>(string id, string indexName) where T : class
    {
        return await _elasticClient.GetAsync<T>(id);
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
        var response = await _elasticClient.SearchAsync<Birthday>(s => s
            .Index(IndexName)
            .Size(size)
            .Query(q => q
                .Raw(queryObject.ToString())
            )
        );

        // Map Elasticsearch document IDs to Birthday.Id properties
        if (response.IsValid && response.Hits.Count > 0)
        {
            foreach (var hit in response.Hits)
            {
                if (hit.Source != null)
                {
                    // Set the Birthday.Id to the Elasticsearch document ID
                    hit.Source.Id = hit.Id;
                }
            }
        }

        return response;
    }

    /// <summary>
    /// Converts a ruleset to an Elasticsearch query
    /// </summary>
    /// <param name="rr">The ruleset to convert</param>
    /// <returns>A JObject containing the Elasticsearch query</returns>
    private async Task<JObject> ConvertRulesetToElasticSearch(RulesetOrRule rr)
    {
        // this routine converts rulesets into elasticsearch DSL as json.  For inexact matching (contains), it uses the field.  For exact matching (=),
        // it uses the keyworkd fields.  Since those are case sensitive, it forces a search for all cased values that would match insenitively
        if (rr.rules == null)
        {
            JObject ret = new JObject{{
                "term", new JObject{{
                    "BOGUSFIELD", "CANTMATCH"
                }}
            }};
            if (rr.@operator != null && rr.@operator.Contains("contains"))
            {
                string stringValue = rr.value?.ToString() ?? string.Empty;
                if (stringValue.StartsWith("/") && (stringValue.EndsWith("/") || stringValue.EndsWith("/i")))
                {
                    Boolean bCaseInsensitive = stringValue.EndsWith("/i");
                    string re = stringValue.Substring(1, stringValue.Length - (bCaseInsensitive ? 3 : 2));
                    string regex = ToElasticRegEx(re.Replace(@"\\",@"\"), bCaseInsensitive);
                    if (regex.StartsWith("^"))
                    {
                        regex = regex.Substring(1, regex.Length - 1);
                    }
                    else
                    {
                        regex = ".*" + regex;
                    }
                    if (regex.EndsWith("$"))
                    {
                        regex = regex.Substring(0, regex.Length - 1);
                    }
                    else
                    {
                        regex += ".*";
                    }
                    if (rr.field == "document")
                    {
                        List<JObject> lstRegexes = "wikipedia,fname,lname,categories,sign,id".Split(',').ToList().Select(s =>
                        {
                            return new JObject{{
                                "regexp", new JObject{{
                                    s + ".keyword", new JObject{
                                        { "value", regex}
                                        ,{ "flags", "ALL" }
                                        ,{ "rewrite", "constant_score" }
                                    }
                                }}
                            }};
                        }).ToList();
                        return new JObject{{
                            "bool", new JObject{{
                                "should", JArray.FromObject(lstRegexes)
                            }}
                        }};
                    }
                    return new JObject{{
                        "regexp", new JObject{{
                            rr.field + ".keyword", new JObject{
                                { "value", regex}
                                ,{ "flags", "ALL" }
                                ,{ "rewrite", "constant_score" }
                            }
                        }}
                    }};
                }
                string quote = Regex.IsMatch(stringValue, @"\W") ? @"""" : "";
                ret = new JObject{{
                    "query_string", new JObject{{
                        "query", (rr.field != "document" ? (rr.field + ":") : "") + quote + stringValue.ToLower().Replace(@"""", @"\""") + quote
                    }}
                }};
            }
            else if (rr.@operator != null && rr.@operator.Contains("="))
            {
                List<string> uniqueValues = (await GetUniqueFieldValuesAsync(rr.field + ".keyword")).ToList();
                string valueToMatch = rr.value?.ToString() ?? string.Empty;
                List<JObject> oredTerms = uniqueValues.Where(v => v.ToLower() == valueToMatch.ToLower()).Select(s =>
                {
                    return new JObject{{
                        "term", new JObject{{
                            rr.field + ".keyword", s
                        }}
                    }};
                }).ToList();
                if (oredTerms.Count > 1)
                {
                    ret = new JObject{{
                        "bool", new JObject{{
                            "should", JArray.FromObject(oredTerms)
                        }}
                    }};
                }
                else if (oredTerms.Count == 1)
                {
                    ret = oredTerms[0];
                }
            } else if (rr.@operator != null && rr.@operator.Contains("in")) {
                List<string> uniqueValues = (await GetUniqueFieldValuesAsync(rr.field + ".keyword")).ToList();
                // The following creates a list of case sensitive possibilities for the case sensitive 'term' query from case insensitive terms
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
                return new JObject{{
                    "terms", new JObject{{
                        rr.field + ".keyword", JArray.FromObject(caseSensitiveMatches)
                    }}
                }};
            } else if (rr.@operator != null && rr.@operator.Contains("exists")) {
                List<JObject> lstExists = new List<JObject>();
                List<JObject> lstEmptyString = new List<JObject>();
                lstEmptyString.Add(new JObject{{
                    "term", new JObject{{
                        rr.field + ".keyword",""
                    }}
                }});
                lstExists.Add(new JObject{{
                    "exists", new JObject{{
                        "field", rr.field
                    }}
                }});
                lstExists.Add(new JObject{{
                    "bool", new JObject{{
                        "must_not", JArray.FromObject(lstEmptyString)
                    }}
                }});
                ret = new JObject{{
                    "bool", new JObject{{
                        "must", JArray.FromObject(lstExists)
                    }}
                }};
            }
            if ((rr.@operator != null && rr.@operator.Contains("!")) || (rr.@operator == "exists" && !(rr.value != null && (Boolean)rr.value))){
                ret = new JObject {{
                    "bool", new JObject{{
                        "must_not", JObject.FromObject(ret)
                    }}
                }};
            }
            return ret;
        }
        else
        {
            List<Object> rls = new List<Object>();
            if (rr.rules != null)
            {
                for (int i = 0; i < rr.rules.Count; i++)
                {
                    rls.Add(await ConvertRulesetToElasticSearch(rr.rules[i]));
                }
            }
            if (rr.condition == "and")
            {
                return new JObject{{
                    "bool", new JObject{{
                        rr.not == true ? "must_not" : "must", JArray.FromObject(rls)
                    }}
                }};
            }
            Object ret = new JObject{{
                "bool", new JObject{{
                    "should", JArray.FromObject(rls)
                }}
            }};
            if (rr.not == true)
            {
                ret = new JObject{{
                    "bool", new JObject{{
                        "must_not", JObject.FromObject(ret)
                    }}
                }};
            }
            return (JObject)ret;
        }
    }

    /// <summary>
    /// Converts a regex pattern to an Elasticsearch-compatible regex
    /// </summary>
    /// <param name="pattern">The regex pattern to convert</param>
    /// <param name="bCaseInsensitive">Whether the regex should be case insensitive</param>
    /// <returns>The Elasticsearch-compatible regex pattern</returns>
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
} 