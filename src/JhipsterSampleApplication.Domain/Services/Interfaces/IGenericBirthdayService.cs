using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using Elasticsearch.Net;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IBirthdayService
    {
        Task<ISearchResponse<Birthday>> SearchAsync(ISearchRequest request);
        Task<IndexResponse> IndexAsync(Birthday birthday);
        Task<DeleteResponse> DeleteAsync(string id);
        Task<UpdateResponse<Birthday>> UpdateAsync(string id, Birthday birthday);
        Task<IReadOnlyCollection<string>> GetUniqueFieldValuesAsync(string field);
        Task<ISearchResponse<Birthday>> SearchWithRulesetAsync(RulesetOrRule ruleset, int size = 10000);
        Task<ISearchResponse<Birthday>> SearchWithLuceneQueryAsync(string query);
    }
} 