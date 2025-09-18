using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Domain.Services.Interfaces;

namespace JhipsterSampleApplication.Domain.Services
{
    public class HistoryService : IHistoryService
    {
        private readonly IHistoryRepository _historyRepository;

        public HistoryService(IHistoryRepository historyRepository)
        {
            _historyRepository = historyRepository;
        }

        public async Task<History> Save(History history)
        {
            if (!string.IsNullOrEmpty(history.User))
            {
                var latest = await _historyRepository.FindLatestByUserAndEntity(history.User!, history.Entity);
                if (latest != null && latest.Text == history.Text)
                {
                    return latest;
                }
            }

            await _historyRepository.CreateOrUpdateAsync(history);
            await _historyRepository.SaveChangesAsync();
            return history;
        }

        public async Task<IEnumerable<History>> FindByUserAndEntity(string user, string? entity = null)
        {
            return await _historyRepository.FindByUserAndEntity(user, entity);
        }
    }
}
