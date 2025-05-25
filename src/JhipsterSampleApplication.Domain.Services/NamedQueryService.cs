using System.Collections.Generic;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using System.Linq;

namespace JhipsterSampleApplication.Domain.Services
{
    public class NamedQueryService : INamedQueryService
    {
        private readonly INamedQueryRepository _namedQueryRepository;

        public NamedQueryService(INamedQueryRepository namedQueryRepository)
        {
            _namedQueryRepository = namedQueryRepository;
        }

        public async Task<NamedQuery> Save(NamedQuery namedQuery)
        {
            await _namedQueryRepository.CreateOrUpdateAsync(namedQuery);
            await _namedQueryRepository.SaveChangesAsync();
            return namedQuery;
        }

        public async Task<IPage<NamedQuery>> FindAll(IPageable pageable)
        {
            return await _namedQueryRepository.FindAllNamedQueries(pageable);
        }

        public async Task<NamedQuery?> FindOne(long id)
        {
            return await _namedQueryRepository.FindOne(id);
        }

        public async Task Delete(long id)
        {
            await _namedQueryRepository.DeleteByIdAsync(id);
            await _namedQueryRepository.SaveChangesAsync();
        }

        public async Task<IEnumerable<NamedQuery>> FindByOwner(string owner)
        {
            List<NamedQuery> owners = (await _namedQueryRepository.FindByOwnerAsync(owner)).ToList();
            List<NamedQuery> global = (await _namedQueryRepository.FindByOwnerAsync("GLOBAL")).ToList();
            owners.AddRange(global.Where(g => !owners.Any(o => o.Name == g.Name)));
            return owners;
        }

        public async Task<IEnumerable<NamedQuery>> FindByName(string name)
        {
            var result = await _namedQueryRepository.QueryHelper()
                .Filter(nq => nq.Name == name)
                .GetAllAsync();
            return result;
        }

        public async Task<IEnumerable<NamedQuery>> FindByNameAndOwner(string name, string owner)
        {
            var result = await _namedQueryRepository.QueryHelper()
                .Filter(nq => nq.Name == name && nq.Owner == owner)
                .GetAllAsync();
            if (!result.Any()){
            result = await _namedQueryRepository.QueryHelper()
                .Filter(nq => nq.Name == name && nq.Owner == "GLOBAL")
                .GetAllAsync();                
            }
            return result;
        }
    }
} 