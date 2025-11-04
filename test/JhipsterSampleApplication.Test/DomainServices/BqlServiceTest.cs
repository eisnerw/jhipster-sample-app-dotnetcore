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
    private readonly Mock<INamedQueryService> _namedQueryServiceMock = new();
    private readonly BqlService<object> _service;

    public BqlServiceTest()
    {
        // Load QB spec from Entities JSON included in test output
        var entitiesPath = Path.Combine(System.AppContext.BaseDirectory, "Resources", "Entities", "birthday.json");
        var qbSpec = JObject.Parse(File.ReadAllText(entitiesPath));
        _namedQueryServiceMock.Setup(s => s.FindByNameAndOwner("JOHNS", null, "birthdays"))
            .ReturnsAsync(new NamedQuery { Id = 1, Name = "JOHNS", Text = "fname = john", Owner = "GLOBAL", Entity = "birthdays" });
        _namedQueryServiceMock.Setup(s => s.FindByNameAndOwner("JOHNSONS", null, "birthdays"))
            .ReturnsAsync(new NamedQuery { Id = 2, Name = "JOHNSONS", Text = "lname = johnson", Owner = "GLOBAL", Entity = "birthdays" });

        _service = new BqlService<object>(NullLogger<BqlService<object>>.Instance, _namedQueryServiceMock.Object,
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

    [Fact]
    public async Task Bql2Ruleset_ShouldUseNegatedOperatorForSingleRule()
    {
        var result = await _service.Bql2Ruleset("!(fname CONTAINS john)");

        Assert.Equal("fname", result.field);
        Assert.Equal("!contains", result.@operator);
        Assert.Equal("john", result.value);
        Assert.False(result.not);
        Assert.Null(result.condition);
        Assert.Empty(result.rules ?? new List<RulesetDto>());
    }

    [Fact]
    public async Task Bql2Ruleset_ShouldPreserveNotForNegatedNamedQuery()
    {
        var result = await _service.Bql2Ruleset("!(JOHNS) & JOHNSONS");

        Assert.Equal("and", result.condition);
        Assert.False(result.not);
        Assert.NotNull(result.rules);
        Assert.Equal(2, result.rules!.Count);

        var negated = result.rules![0];
        Assert.Equal("fname", negated.field);
        Assert.Equal("!=", negated.@operator);
        Assert.Equal("john", negated.value);
        Assert.False(negated.not);

        var positive = result.rules![1];
        Assert.Equal("lname", positive.field);
        Assert.Equal("=", positive.@operator);
        Assert.Equal("johnson", positive.value);
        Assert.False(positive.not);
    }
}
