using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JhipsterSampleApplication.Test.DomainServices;

public class BirthdayBqlServiceTest
{
    private readonly BirthdayBqlService _service;

    public BirthdayBqlServiceTest()
    {
        var namedQueryService = new Mock<INamedQueryService>().Object;
        _service = new BirthdayBqlService(NullLogger<BirthdayBqlService>.Instance, namedQueryService);
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
