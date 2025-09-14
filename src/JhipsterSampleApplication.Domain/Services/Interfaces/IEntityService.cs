using System.Threading.Tasks;
using System.Collections.Generic;
using JhipsterSampleApplication.Domain.Search;
using JhipsterSampleApplication.Domain.Entities;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IEntityService<T> where T : class
    {
        Task<AppSearchResponse<T>> SearchAsync(SearchSpec<T> spec);
        Task<WriteResult> IndexAsync(T document);
        Task<WriteResult> UpdateAsync(string id, T document);
        Task<WriteResult> DeleteAsync(string id);
        Task<List<string>> GetUniqueFieldValuesAsync(string field);
        Task<AppSearchResponse<T>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, string? sort = null);
        Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr);
        Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto view, int size = 20, int from = 0, string? sort = null);
        Task<SimpleApiResponse> CategorizeAsync(CategorizeRequestDto request);
        Task<SimpleApiResponse> CategorizeMultipleAsync(CategorizeMultipleRequestDto request);
        Task<ClusterHealthDto> GetHealthAsync();
    }
} 
