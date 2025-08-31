using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JhipsterSampleApplication.Test.DomainServices;

public class SupremeBqlServiceTest
{
    private readonly SupremeBqlService _service;

    public SupremeBqlServiceTest()
    {
        var namedQueryService = new Mock<INamedQueryService>().Object;
        _service = new SupremeBqlService(NullLogger<SupremeBqlService>.Instance, namedQueryService);
    }

    [Fact]
    public async Task Ruleset2Bql_ShouldLowercaseSearchTerms()
    {
        var ruleset = new RulesetDto
        {
            condition = "and",
            rules = new List<RulesetDto>
            {
                new RulesetDto { field = "document", @operator = "contains", value = "WOODY" }
            }
        };

        var result = await _service.Ruleset2Bql(ruleset);

        Assert.Equal("woody", result);
    }
}
