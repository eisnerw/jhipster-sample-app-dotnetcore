using JhipsterSampleApplication.Domain.Entities;
using Xunit;

namespace JhipsterSampleApplication.Test.Domain;

public class ViewEntityTest
{
    [Fact]
    public void EnsureId_DoesNotOverwriteProvidedId()
    {
        var view = new View
        {
            Id = "term",
            Name = "Term/heard by",
            Domain = "supreme"
        };

        view.EnsureId();

        Assert.Equal("term", view.Id);
    }

    [Fact]
    public void EnsureId_GeneratesHierarchicalIdWhenMissing()
    {
        var view = new View
        {
            Name = "Heard By",
            parentViewId = "term",
            Domain = "supreme"
        };

        view.EnsureId();

        Assert.Equal("term/heard by", view.Id);
    }
}

