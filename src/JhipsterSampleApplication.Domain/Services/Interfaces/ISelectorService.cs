using System;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface ISelectorService
    {
        Task<Selector> Save(Selector selector);

        Task<IPage<Selector>> FindAll(IPageable pageable);

        Task<Selector> FindOne(long id);

        Task Delete(long id);
    }
}
