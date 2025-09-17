using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace JhipsterSampleApplication.Test.DomainServices;

public class BqlServiceTest
{
    private readonly BqlService<Birthday> _service;

    public BqlServiceTest()
    {
        var namedQueryService = new Mock<INamedQueryService>().Object;
        // Load QB spec from Entities JSON included in test output
        var entitiesPath = Path.Combine(System.AppContext.BaseDirectory, "Resources", "Entities", "birthday.json");
        var qbSpec = JObject.Parse(File.ReadAllText(entitiesPath))["queryBuilder"] as JObject ?? new JObject();
        _service = new BqlService<Birthday>(NullLogger<BqlService<Birthday>>.Instance, namedQueryService,
            qbSpec, "birthdays");
    }

    [Theory]
    [InlineData("/ani/")]
    [InlineData("/dani/i")]
    public async Task Ruleset2Bql_ShouldReturnRegexWithoutQuotes(string pattern)
    {
        var ruleset = new RulesetDto
        {
            condition = "and",
            rules = new List<RulesetDto>
            {
                new RulesetDto { field = "document", @operator = "like", value = pattern }
            }
        };

        var result = await _service.Ruleset2Bql(ruleset);

        Assert.Equal(pattern, result);
    }
}
