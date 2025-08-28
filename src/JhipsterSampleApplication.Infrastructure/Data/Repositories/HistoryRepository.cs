using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;

namespace JhipsterSampleApplication.Infrastructure.Data.Repositories
{
    public class HistoryRepository : GenericRepository<History, long>, IHistoryRepository
    {
        public HistoryRepository(IUnitOfWork context) : base(context)
        {
        }

        public async Task<IEnumerable<History>> FindByUserAndDomain(string user, string domain)
        {
            return await QueryHelper()
                .Filter(h => h.User == user && h.Domain == domain)
                .OrderBy(q => q.OrderByDescending(h => h.Id))
                .GetAllAsync();
        }
    }
}
