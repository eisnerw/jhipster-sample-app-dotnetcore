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
            await _historyRepository.CreateOrUpdateAsync(history);
            await _historyRepository.SaveChangesAsync();
            return history;
        }

        public async Task<IEnumerable<History>> FindByUserAndDomain(string user, string domain)
        {
            return await _historyRepository.FindByUserAndDomain(user, domain);
        }
    }
}
