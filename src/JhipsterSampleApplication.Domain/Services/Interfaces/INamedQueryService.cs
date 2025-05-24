using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using System.Collections.Generic;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface INamedQueryService
    {
        Task<NamedQuery> Save(NamedQuery namedQuery);

        Task<IPage<NamedQuery>> FindAll(IPageable pageable);

        Task<NamedQuery?> FindOne(long id);

        Task Delete(long id);

        Task<IEnumerable<NamedQuery>> FindByOwner(string owner);

        Task<IEnumerable<NamedQuery>> FindByName(string name);

        Task<IEnumerable<NamedQuery>> FindByNameAndOwner(string name, string owner);
    }
} 