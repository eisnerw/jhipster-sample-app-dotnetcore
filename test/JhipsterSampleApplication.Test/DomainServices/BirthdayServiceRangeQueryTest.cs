using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Nest;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Xunit;

namespace JhipsterSampleApplication.Test.DomainServices;

public class BirthdayServiceRangeQueryTest
{
    private readonly EntityService<Birthday> _service;

    public BirthdayServiceRangeQueryTest()
    {
        var elasticClient = new Mock<IElasticClient>().Object;
        var bqlService = new BqlService<Birthday>(new Mock<ILogger<BqlService<Birthday>>>().Object,
            new Mock<INamedQueryService>().Object, new JObject(), "birthdays");
        var viewService = new Mock<IViewService>().Object;
        _service = new EntityService<Birthday>("birthdays", "wikipedia", elasticClient, bqlService, viewService);
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

        var result = await _service.ConvertRulesetToElasticSearch(ruleset);

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

        var result = await _service.ConvertRulesetToElasticSearch(ruleset);

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

        var result = await _service.ConvertRulesetToElasticSearch(ruleset);

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

        var result = await _service.ConvertRulesetToElasticSearch(ruleset);

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
