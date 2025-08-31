using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Repositories.Interfaces
{
    public interface IHistoryRepository : IGenericRepository<History, long>
    {
        Task<IEnumerable<History>> FindByUserAndDomain(string user, string? domain = null);
        Task<History?> FindLatestByUserAndDomain(string user, string? domain = null);
    }
}
