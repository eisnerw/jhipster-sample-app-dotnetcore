using Nest;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IQueryBuilder
    {
        IQueryBuilder WithRuleset(RulesetOrRule ruleset);
        IQueryBuilder WithPagination(int page, int size);
        IQueryBuilder WithSort(string field, bool ascending);
        IQueryBuilder WithFilter(string field, object value);
        SearchDescriptor<Birthday> Build();
    }
} 