using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using JhipsterSampleApplication.Domain.Entities;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IEntityService<T> where T : class
    {
        Task<SearchResponse<T>> SearchAsync(object request, bool includeDetails, string? pitId = null);
        Task<IndexResponse> IndexAsync(T document);
        Task<UpdateResponse<T>> UpdateAsync(string id, T document);
        Task<DeleteResponse> DeleteAsync(string id);
        Task<List<string>> GetUniqueFieldValuesAsync(string field);
        Task<SearchResponse<T>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0);
        Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr);
        Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto view, int size = 20, int from = 0);
        Task<List<ViewResultDto>> SearchUsingViewAsync(object request, object uncategorizedRequest);
        Task<SimpleApiResponse> CategorizeAsync(CategorizeRequestDto request);
        Task<SimpleApiResponse> CategorizeMultipleAsync(CategorizeMultipleRequestDto request);
    }
} 