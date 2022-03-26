using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using Jhipster.Domain;

namespace Jhipster.Domain.Services.Interfaces
{
    public interface ISelectorService
    {
        Task<Selector> Save(Selector selector);

        Task<IPage<Selector>> FindAll(IPageable pageable);

        Task<Selector> FindOne(long id);

        Task Delete(long id);
    }
}
