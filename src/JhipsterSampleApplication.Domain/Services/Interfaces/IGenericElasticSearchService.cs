using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using JhipsterSampleApplication.Domain.Entities;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IGenericElasticSearchService<T> where T : class
    {
        Task<ISearchResponse<T>> SearchAsync(ISearchRequest request);
        Task<ISearchResponse<T>> SearchWithLuceneQueryAsync(string luceneQuery, int from = 0, int size = 20);
        Task<List<ViewResultDto>> SearchWithLuceneQueryAndViewAsync(string luceneQuery, ViewDto viewDto,int from = 0, int size = 20);
        Task<IndexResponse> IndexAsync(T document);
        Task<UpdateResponse<T>> UpdateAsync(string id, T document);
        Task<DeleteResponse> DeleteAsync(string id);
        Task<List<string>> GetUniqueFieldValuesAsync(string field);
        Task<ISearchResponse<T>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null);
        Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr);
        Task<List<ViewResultDto>> SearchWithElasticQueryAndViewAsync(JObject queryObject, ViewDto view, int size = 20, int from = 0, IList<ISort>? sort = null);
        Task<List<ViewResultDto>> SearchUsingViewAsync(ISearchRequest request, ISearchRequest uncategorizedRequest);
    }

    public interface IGenericElasticSearchService : IGenericElasticSearchService<Birthday>
    {
    }
} 