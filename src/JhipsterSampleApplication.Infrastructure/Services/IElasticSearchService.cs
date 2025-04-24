using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using Elasticsearch.Net;

namespace JhipsterSampleApplication.Infrastructure.Services;

public interface IGenericElasticSearchService
{
    Task<Nest.ISearchResponse<T>> SearchAsync<T>(ISearchRequest searchRequest) where T : class;
    Task<Nest.IndexResponse> IndexAsync<T>(T document, string indexName) where T : class;
    Task<Nest.DeleteResponse> DeleteAsync<T>(string id, string indexName) where T : class;
    Task<Nest.IGetResponse<T>> GetAsync<T>(string id, string indexName) where T : class;
} 