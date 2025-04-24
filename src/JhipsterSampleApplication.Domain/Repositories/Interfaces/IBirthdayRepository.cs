using System.Threading.Tasks;
using System.Collections.Generic;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Repositories.Interfaces
{
    public interface IBirthdayRepository : IGenericRepository<Birthday, long>
    {
        Task<Birthday?> GetOneAsync(object id, bool bText);

        Task<string> GetOneTextAsync(object id);

        Task<List<Birthday>?> GetReferencesFromAsync(string id);

        Task<List<Birthday>> GetReferencesToAsync(string id);

        Task<List<string>> GetUniqueFieldValuesAsync(string field);

        Task<IPage<Birthday>> GetPageFilteredAsync(IPageable pageable, string query);
    }
}
