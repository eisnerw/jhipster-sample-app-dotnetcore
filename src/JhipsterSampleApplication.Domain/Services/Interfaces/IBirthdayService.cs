using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using JhipsterSampleApplication.Domain.Entities;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IBirthdayService : IGenericElasticSearchService<Birthday>
    {
        Task<string?> GetHtmlByIdAsync(string id);
        Task<object> Search(JObject elasticsearchQuery, int pageSize = 20, int from = 0, string? sort = null,
            bool includeDetails = false, string? view = null, string? category = null,
            string? secondaryCategory = null, string? pitId = null, string[]? searchAfter = null);
        Task<BirthdayDto?> GetByIdAsync(string id, bool includeDetails = false);
        Task<SimpleApiResponse> CategorizeAsync(CategorizeRequestDto request);
        Task<SimpleApiResponse> CategorizeMultipleAsync(CategorizeMultipleRequestDto request);
    }
}