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
    public async Task<ISearchResponse<Birthday>> SearchAsync(ISearchRequest request)
    {
        var response = await _elasticClient.SearchAsync<Birthday>(request);
        if (!response.IsValid){
            throw new Exception(response.DebugInformation);
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
        ((BucketAggregate)result.Aggregations.ToList()[0].Value).Items.ToList().ForEach(it =>
        {
            KeyedBucket<Object> kb = (KeyedBucket<Object>)it;
            string categoryName = kb.KeyAsString != null ? kb.KeyAsString : (string)kb.Key;
            if (Regex.IsMatch(categoryName, @"\d{4,4}-\d{2,2}-\d{2,2}T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z"))
            {
                categoryName = Regex.Replace(categoryName, @"(\d{4,4})-(\d{2,2})-(\d{2,2})T\d{2,2}:\d{2,2}:\d{2,2}.\d{3,3}Z", "$1-$2-$3");
            }
            content.Add(new ViewResultDto
            {
                CategoryName = categoryName,
                Count = kb.DocCount
            });

        });
        content = content.OrderBy(cat => cat.CategoryName).ToList();
        var uncategorizedResponse = await _elasticClient.SearchAsync<Birthday>(uncategorizedRequest);
        var uncatetgorizedCount = uncategorizedResponse.Aggregations.Filter("uncategorized").DocCount;
        if (uncatetgorizedCount > 0)
        {
            content.Add(new ViewResultDto
            {
                CategoryName = "(Uncategorized)",
                Selected = false,
                NotCategorized = true,
                Count = uncatetgorizedCount
            });
        }
        return content;
    }

    /// </summary>
    /// <param name="luceneQuery">The search request to execute</param>
    
    /// <param name="from">The starting index for pagination</param>
    /// <param name="size">The number of documents to return</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<List<ViewResultDto>> SearchWithLuceneQueryAndViewAsync(string luceneQuery, ViewDto viewDto, int from = 0, int size = 20)
    {
        var request = new SearchRequest<Birthday>
        {
            Size = 0,
            From = from,
            Query = new QueryStringQuery
            {
                Query = luceneQuery
            },
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
            Query = new QueryStringQuery
            {
                Query = luceneQuery
            },
            Aggregations = new AggregationDictionary{
                {

                    "uncategorized", new FilterAggregation("uncategorized")
                    {
                        Filter = new BoolQuery
                        {
                            MustNot = new List<QueryContainer>
                            {
                                new ExistsQuery
                                {
                                    Field = viewDto.Aggregation
                                }
                            }
                        }
                    }

                }
            }
        };        
        return await SearchUsingViewAsync(request, uncategorizedRequest);
    }


    /// <summary>
    /// Searches for Birthday documents using the provided search request in lucene format
    /// </summary>
    /// <param name="luceneQuery">The search request to execute</param>
    /// <param name="from">The starting index for pagination</param>
    /// <param name="size">The number of documents to return</param>
    /// <returns>The search response containing Birthday documents</returns>
    public async Task<ISearchResponse<Birthday>> SearchWithLuceneQueryAsync(string luceneQuery, int from = 0, int size = 20)
    {
        var response = await _elasticClient.SearchAsync<Birthday>(s => s
            .Index("birthdays")
            .From(from)
            .Size(size)
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
        // this routine converts rulesets into elasticsearch DSL as json.  For inexact matching (contains), it uses the field.  For exact matching (=),
        // it uses the keyworkd fields.  Since those are case sensitive, it forces a search for all cased values that would match insenitively
        if (rr.rules == null || rr.rules.Count == 0)
        {
            JObject ret = new JObject{{
                "term", new JObject{{
                    "BOGUSFIELD", "CANTMATCH"
                }}
            }};
            if (rr.@operator?.Contains("contains") == true)
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
            else if (rr.@operator?.Contains("=") == true)
            {
                List<string> uniqueValues = await GetUniqueFieldValuesAsync(rr.field + ".keyword");
                string valueStr = rr.value?.ToString() ?? string.Empty;
                List<JObject> oredTerms = uniqueValues.Where(v => v.ToLower() == valueStr.ToLower()).Select(s =>
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
            } else if (rr.@operator?.Contains("in") == true) {
                List<string> uniqueValues = await GetUniqueFieldValuesAsync(rr.field + ".keyword");
                var valueArray = rr.value as JArray ?? new JArray();
                List<string> caseSensitiveMatches = valueArray.Select(v =>
                {
                    string vStr = v?.ToString() ?? string.Empty;
                    return uniqueValues.Where(s => s.ToLower() == vStr.ToLower());
                }).Aggregate((agg, list) => agg.Concat(list).ToList()).ToList();
                return new JObject{{
                    "terms", new JObject{{
                        rr.field + ".keyword", JArray.FromObject(caseSensitiveMatches)
                    }}
                }};
            } else if (rr.@operator?.Contains("exists") == true) {
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
            if (rr.@operator?.Contains("!") == true || (rr.@operator == "exists" && !(rr.value != null && (Boolean)rr.value)))
            {
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
            for (int i = 0; i < rr.rules.Count; i++)
            {
                rls.Add(await ConvertRulesetToElasticSearch(rr.rules[i]));
            }
            if (rr.condition == "and")
            {
                return new JObject{{
                    "bool", new JObject{{
                        rr.not == true ? "must_not" : "must", JArray.FromObject(rls)
                    }}
                }};
            }
            JObject ret = new JObject{{
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
            return ret;
        }
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
                            MustNot = new List<QueryContainer>
                            {
                                new ExistsQuery
                                {
                                    Field = viewDto.Aggregation
                                }
                            }
                        }
                    }

                }
            }
        };
        return await SearchUsingViewAsync(request, uncategorizedRequest);
    }
} 