using System.Threading.Tasks;
using System.Collections.Generic;
using JhipsterSampleApplication.Domain.Search;
using JhipsterSampleApplication.Domain.Entities;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IEntityService
    {
        Task<AppSearchResponse<JObject>> SearchAsync(string entity, SearchSpec<JObject> spec);
        Task<WriteResult> IndexAsync(string entity, JObject document);
        Task<WriteResult> UpdateAsync(string entity, string id, JObject document);
        Task<WriteResult> DeleteAsync(string entity, string id);
        Task<List<string>> GetUniqueFieldValuesAsync(string entity, string field);
        Task<JObject> ConvertRulesetToElasticSearch(string entity, Ruleset rr);
        Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(string entity, JObject queryObject, ViewDto view, int size = 20, int from = 0, string? sort = null);
        Task<SimpleApiResponse> CategorizeAsync(string entity, CategorizeRequestDto request);
        Task<ClusterHealthDto> GetHealthAsync();
    }
} 
