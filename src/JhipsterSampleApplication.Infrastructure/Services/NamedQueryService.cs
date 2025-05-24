using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace JhipsterSampleApplication.Infrastructure.Services
{
    public class NamedQueryService : INamedQueryService
    {
        private readonly ILogger<NamedQueryService> _log;
        private readonly INamedQueryRepository _namedQueryRepository;

        public NamedQueryService(ILogger<NamedQueryService> log, INamedQueryRepository namedQueryRepository)
        {
            _log = log;
            _namedQueryRepository = namedQueryRepository;
        }

        public async Task<NamedQuery> Save(NamedQuery namedQuery)
        {
            _log.LogDebug($"Request to save NamedQuery : {namedQuery}");
            var result = await _namedQueryRepository.CreateOrUpdateAsync(namedQuery);
            await _namedQueryRepository.SaveChangesAsync();
            return result;
        }

        public async Task<IPage<NamedQuery>> FindAll(IPageable pageable)
        {
            _log.LogDebug($"Request to get all NamedQueries");
            var result = await _namedQueryRepository.QueryHelper()
                .GetPageAsync(pageable);
            return result;
        }

        public async Task<NamedQuery?> FindOne(long id)
        {
            _log.LogDebug($"Request to get NamedQuery : {id}");
            var result = await _namedQueryRepository.QueryHelper()
                .GetOneAsync(namedQuery => namedQuery.Id == id);
            return result;
        }

        public async Task Delete(long id)
        {
            _log.LogDebug($"Request to delete NamedQuery : {id}");
            await _namedQueryRepository.DeleteByIdAsync(id);
            await _namedQueryRepository.SaveChangesAsync();
        }

        public async Task<IEnumerable<NamedQuery>> FindByOwner(string owner)
        {
            _log.LogDebug($"Request to get NamedQueries by owner : {owner}");
            return await _namedQueryRepository.FindByOwnerAsync(owner);
        }
    }
} 