using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Moq;
using Nest;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JhipsterSampleApplication.Test.DomainServices;

public class BirthdayServiceRangeQueryTest
{
    private readonly BirthdayService _service;

    public BirthdayServiceRangeQueryTest()
    {
        var elasticClient = new Mock<IElasticClient>().Object;
        var bqlService = new Mock<IBirthdayBqlService>().Object;
        var viewService = new Mock<IViewService>().Object;
        _service = new BirthdayService(elasticClient, bqlService, viewService);
    }

    [Theory]
    [InlineData(">", "gt")]
    [InlineData(">=", "gte")]
    [InlineData("<", "lt")]
    [InlineData("<=", "lte")]
    public async Task ConvertRulesetToElasticSearch_HandlesRangeOperators(string op, string expected)
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
                        new JObject { { expected, "1990-01-01" } }
                    }
                }
            }
        };

        Assert.True(JToken.DeepEquals(expectedObject, result));
    }
}
