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

        public async Task<IEnumerable<History>> FindAll()
        {
            return await QueryHelper()
                .OrderBy(q => q.OrderByDescending(h => h.Id))
                .GetAllAsync();
        }

        public async Task<IEnumerable<History>> FindByUserAndEntity(string user, string? entity = null)
        {
            if (string.IsNullOrEmpty(entity))
            {
                return await QueryHelper()
                    .Filter(h => h.User == user)
                    .OrderBy(q => q.OrderByDescending(h => h.Id))
                    .GetAllAsync();
            }

            return await QueryHelper()
                .Filter(h => h.User == user && h.Entity == entity)
                .OrderBy(q => q.OrderByDescending(h => h.Id))
                .GetAllAsync();
        }

        public async Task<History?> FindLatestByUserAndEntity(string user, string? entity = null)
        {
            var histories = await FindByUserAndEntity(user, entity);
            return histories.FirstOrDefault();
        }
    }
}
