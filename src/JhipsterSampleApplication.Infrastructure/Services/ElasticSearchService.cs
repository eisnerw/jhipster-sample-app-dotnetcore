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

public class ElasticSearchService : IElasticSearchService, IGenericElasticSearchService
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "birthdays";

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

    // IElasticSearchService implementation
    public async Task<ISearchResponse<Birthday>> SearchAsync(ISearchRequest request)
    {
        return await _elasticClient.SearchAsync<Birthday>(request);
    }

    public async Task<IndexResponse> IndexAsync(Birthday birthday)
    {
        return await _elasticClient.IndexDocumentAsync(birthday);
    }

    public async Task<DeleteResponse> DeleteAsync(string id)
    {
        return await _elasticClient.DeleteAsync<Birthday>(id);
    }

    public async Task<UpdateResponse<Birthday>> UpdateAsync(string id, Birthday birthday)
    {
        return await _elasticClient.UpdateAsync<Birthday>(id, u => u.Doc(birthday));
    }

    // IGenericElasticSearchService implementation
    public async Task<ISearchResponse<T>> SearchAsync<T>(ISearchRequest searchRequest) where T : class
    {
        return await _elasticClient.SearchAsync<T>(searchRequest);
    }

    public async Task<IndexResponse> IndexAsync<T>(T document, string indexName) where T : class
    {
        return await _elasticClient.IndexDocumentAsync(document);
    }

    public async Task<DeleteResponse> DeleteAsync<T>(string id, string indexName) where T : class
    {
        return await _elasticClient.DeleteAsync<T>(id);
    }

    public async Task<IGetResponse<T>> GetAsync<T>(string id, string indexName) where T : class
    {
        return await _elasticClient.GetAsync<T>(id);
    }

    public async Task<IReadOnlyCollection<string>> GetUniqueFieldValuesAsync(string field)
    {
        var response = await _elasticClient.SearchAsync<Birthday>(s => s
            .Size(0)
            .Index(IndexName)
            .Aggregations(a => a
                .Terms("distinct", t => t
                    .Field(field)
                    .Size(10000)
                )
            )
        );

        var termsAggregation = response.Aggregations.Terms("distinct");
        var values = new List<string>();
        
        foreach (var bucket in termsAggregation.Buckets)
        {
            values.Add(bucket.KeyAsString);
        }

        return values;
    }

    public async Task<ISearchResponse<Birthday>> SearchWithRulesetAsync(RulesetOrRule ruleset, int size = 10000)
    {
        var queryObject = await ConvertRulesetToElasticSearch(ruleset);
        string query = queryObject.ToString();
        return await _elasticClient.SearchAsync<Birthday>(s => s
            .Index(IndexName)
            .Size(size)
            .Query(q => q
                .Raw(queryObject.ToString())
            )
        );
    }

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
            if (rr.@operator.Contains("contains"))
            {
                string stringValue = (string)rr.value;
                if (stringValue.StartsWith("/") && (stringValue.EndsWith("/") || stringValue.EndsWith("/i")))
                {
                    Boolean bCaseInsensitive = stringValue.EndsWith("/i");
                    string re = rr.value.ToString().Substring(1, rr.value.ToString().Length - (bCaseInsensitive ? 3 : 2));
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
                string quote = Regex.IsMatch(rr.value.ToString(), @"\W") ? @"""" : "";
                ret = new JObject{{
                    "query_string", new JObject{{
                        "query", (rr.field != "document" ? (rr.field + ":") : "") + quote + ((string)rr.value).ToLower().Replace(@"""", @"\""") + quote
                    }}
                }};
            }
            else if (rr.@operator.Contains("="))
            {
                List<string> uniqueValues = (await GetUniqueFieldValuesAsync(rr.field + ".keyword")).ToList();
                List<JObject> oredTerms = uniqueValues.Where(v => v.ToLower() == rr.value.ToString().ToLower()).Select(s =>
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
            } else if (rr.@operator.Contains("in")) {
                List<string> uniqueValues = (await GetUniqueFieldValuesAsync(rr.field + ".keyword")).ToList();
                // The following creates a list of case sensitive possibilities for the case sensitive 'term' query from case insensitive terms
                List<string> caseSensitiveMatches = ((JArray)rr.value).Select(v =>
                {
                    return uniqueValues.Where(s => s.ToLower() == v.ToString().ToLower());
                }).Aggregate((agg,list) => {
                    return agg.Concat(list).ToList();
                }).ToList();
                return new JObject{{
                    "terms", new JObject{{
                        rr.field + ".keyword", JArray.FromObject(caseSensitiveMatches)
                    }}
                }};
            } else if (rr.@operator.Contains("exists")) {
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
            if (rr.@operator.Contains("!") || (rr.@operator == "exists" && !(rr.value != null && (Boolean)rr.value))){
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