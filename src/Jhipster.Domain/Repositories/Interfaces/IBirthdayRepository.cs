using System.Threading.Tasks;
using System.Collections.Generic;

namespace Jhipster.Domain.Repositories.Interfaces
{
    public interface IBirthdayRepository : IGenericRepository<Birthday>
    {
        Task<Birthday> GetOneAsync(object id, bool bText);

        Task<string> GetOneTextAsync(object id);

        Task<List<Birthday>> GetReferencesFromAsync(string id);

        Task<List<Birthday>> GetReferencesToAsync(string id);

        Task<List<string>> GetUniqueFieldValuesAsync(string field);
    }
}
