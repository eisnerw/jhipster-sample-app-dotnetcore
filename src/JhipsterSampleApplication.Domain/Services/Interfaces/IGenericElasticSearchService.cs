using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IGenericElasticSearchService<T> where T : class
    {
        Task<ISearchResponse<T>> SearchAsync(ISearchRequest request);
        Task<ISearchResponse<T>> SearchWithLuceneQueryAsync(string luceneQuery);
        Task<IndexResponse> IndexAsync(T document);
        Task<UpdateResponse<T>> UpdateAsync(string id, T document);
        Task<DeleteResponse> DeleteAsync(string id);
        Task<IReadOnlyCollection<string>> GetUniqueFieldValuesAsync(string field);
        Task<ISearchResponse<T>> SearchWithRulesetAsync(RulesetOrRule ruleset, int size = 10000);
    }

    public interface IGenericElasticSearchService : IGenericElasticSearchService<Birthday>
    {
    }
} 