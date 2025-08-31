using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IHistoryService
    {
        Task<History> Save(History history);
        Task<IEnumerable<History>> FindByUserAndDomain(string user, string? domain = null);
    }
}
