using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Repositories.Interfaces
{
    public interface IHistoryRepository : IGenericRepository<History, long>
    {
        Task<IEnumerable<History>> FindByUserAndEntity(string user, string? entity = null);
        Task<History?> FindLatestByUserAndEntity(string user, string? entity = null);
    }
}
