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
        return await _elasticClient.SearchAsync<Birthday>(s => s
            .Index(IndexName)
            .Size(size)
            .Query(q => q
                .Raw(queryObject.ToString())
            )
        );
    }

    private async Task<JObject> ConvertRulesetToElasticSearch(RulesetOrRule ruleset)
    {
        if (ruleset.rules == null)
        {
            if (ruleset.@operator?.Contains("contains") == true)
            {
                string stringValue = (string)ruleset.value!;
                if (stringValue.StartsWith("/") && (stringValue.EndsWith("/") || stringValue.EndsWith("/i")))
                {
                    bool isCaseInsensitive = stringValue.EndsWith("/i");
                    string regex = stringValue.Substring(1, stringValue.Length - (isCaseInsensitive ? 3 : 2));
                    return new JObject
                    {
                        ["regexp"] = new JObject
                        {
                            [ruleset.field!] = new JObject
                            {
                                ["value"] = regex,
                                ["case_insensitive"] = isCaseInsensitive
                            }
                        }
                    };
                }
            }

            return new JObject
            {
                ["term"] = new JObject
                {
                    [ruleset.field!] = JToken.FromObject(ruleset.value!)
                }
            };
        }

        var boolQuery = new JObject();
        var rules = new JArray();

        foreach (var rule in ruleset.rules)
        {
            rules.Add(await ConvertRulesetToElasticSearch(rule));
        }

        var boolObject = new JObject();
        var queryType = ruleset.condition?.ToLower() == "and" ? "must" : "should";
        
        if (ruleset.@not)
        {
            boolObject["must_not"] = rules;
        }
        else
        {
            boolObject[queryType] = rules;
        }

        boolQuery["bool"] = boolObject;

        return boolQuery;
    }
} 