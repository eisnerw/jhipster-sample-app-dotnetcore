using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Elastic.Clients.Elasticsearch;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace JhipsterSampleApplication.Test.DomainServices;

public class BirthdayServiceRangeQueryTest
{
    private readonly EntityService _service;

    public BirthdayServiceRangeQueryTest()
    {
        // Minimal DI container with IConfiguration and ElasticsearchClient
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string>
        {
            ["Elasticsearch:Url"] = "http://localhost:9200",
            ["Elasticsearch:Username"] = "",
            ["Elasticsearch:Password"] = ""
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        services.AddSingleton(configuration);

        var node = new Uri(configuration["Elasticsearch:Url"]!);
        var clientSettings = new ElasticsearchClientSettings(node);
        var esClient = new ElasticsearchClient(clientSettings);
        services.AddSingleton(esClient);

        var serviceProvider = services.BuildServiceProvider();
        var namedQueryService = new Mock<INamedQueryService>().Object;
        // Use real spec registry backed by Resources/Entities copied into test output
        var specRegistry = new JhipsterSampleApplication.Domain.Services.EntitySpecRegistry(configuration);
        _service = new EntityService(serviceProvider, namedQueryService, specRegistry);
    }

    [Theory]
    [InlineData(">", "gte", "1990-01-02T00:00:00")]
    [InlineData(">=", "gte", "1990-01-01T00:00:00")]
    [InlineData("<", "lt", "1990-01-01T00:00:00")]
    [InlineData("<=", "lt", "1990-01-02T00:00:00")]
    public async Task ConvertRulesetToElasticSearch_HandlesRangeOperators(string op, string expected, string value)
    {
        var ruleset = new Ruleset
        {
            field = "dob",
            @operator = op,
            value = "1990-01-01"
        };

        var result = await _service.ConvertRulesetToElasticSearch("birthday", ruleset);

        var expectedObject = new JObject
        {
            {
                "range",
                new JObject
                {
                    {
                        "dob",
                        new JObject { { expected, value } }
                    }
                }
            }
        };

        Assert.True(JToken.DeepEquals(expectedObject, result));
    }

    [Fact]
    public async Task ConvertRulesetToElasticSearch_PreservesDateTimeValues()
    {
        var ruleset = new Ruleset
        {
            field = "dob",
            @operator = ">",
            value = "1990-01-01T12:34:56"
        };

        var result = await _service.ConvertRulesetToElasticSearch("birthday", ruleset);

        var expectedObject = new JObject
        {
            {
                "range",
                new JObject
                {
                    {
                        "dob",
                        new JObject { { "gt", "1990-01-01T12:34:56" } }
                    }
                }
            }
        };

        Assert.True(JToken.DeepEquals(expectedObject, result));
    }

    [Theory]
    [InlineData("1990", "1990-01-01T00:00:00", "1991-01-01T00:00:00")]
    [InlineData("1990-02", "1990-02-01T00:00:00", "1990-03-01T00:00:00")]
    [InlineData("1990-03-03", "1990-03-03T00:00:00", "1990-03-04T00:00:00")]
    public async Task ConvertRulesetToElasticSearch_HandlesDateEqualityRanges(string value, string gte, string lt)
    {
        var ruleset = new Ruleset
        {
            field = "dob",
            @operator = "=",
            value = value,
        };

        var result = await _service.ConvertRulesetToElasticSearch("birthday", ruleset);

        var expectedObject = new JObject
        {
            {
                "range",
                new JObject
                {
                    {
                        "dob",
                        new JObject { { "gte", gte }, { "lt", lt } }
                    }
                }
            }
        };

        Assert.True(JToken.DeepEquals(expectedObject, result));
    }

    [Theory]
    [InlineData(">=", "1990", "gte", "1990-01-01T00:00:00")]
    [InlineData("<=", "1990", "lt", "1991-01-01T00:00:00")]
    [InlineData(">", "1990-03-03", "gte", "1990-03-04T00:00:00")]
    [InlineData("<", "1990-03-03", "lt", "1990-03-03T00:00:00")]
    public async Task ConvertRulesetToElasticSearch_HandlesPartialDateInequalities(string op, string value, string expectedOp, string expectedValue)
    {
        var ruleset = new Ruleset
        {
            field = "dob",
            @operator = op,
            value = value,
        };

        var result = await _service.ConvertRulesetToElasticSearch("birthday", ruleset);

        var expectedObject = new JObject
        {
            {
                "range",
                new JObject
                {
                    {
                        "dob",
                        new JObject { { expectedOp, expectedValue } }
                    }
                }
            }
        };

        Assert.True(JToken.DeepEquals(expectedObject, result));
    }
}
