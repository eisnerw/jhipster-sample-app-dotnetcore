using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using JhipsterSampleApplication.Domain.Entities;
using System.Collections.Generic;

namespace JhipsterSampleApplication.Domain.Repositories.Interfaces
{
    public interface INamedQueryRepository : IGenericRepository<NamedQuery, long>
    {
        new Task<NamedQuery> CreateOrUpdateAsync(NamedQuery namedQuery);
        Task<IPage<NamedQuery>> FindAllNamedQueries(IPageable pageable);
        Task<NamedQuery> FindOneByName(string name, string? domain = null);
        Task<List<NamedQuery>> FindByOwnerAsync(string owner, string? domain = null);
        Task<NamedQuery> FindOne(long id);
    }
}