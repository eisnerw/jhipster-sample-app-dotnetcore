using System;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JhipsterSampleApplication.Domain.Services;

public class SelectorService : ISelectorService
{
    protected readonly ISelectorRepository _selectorRepository;

    public SelectorService(ISelectorRepository selectorRepository)
    {
        _selectorRepository = selectorRepository;
    }

    public virtual async Task<Selector> Save(Selector selector)
    {
        await _selectorRepository.CreateOrUpdateAsync(selector);
        await _selectorRepository.SaveChangesAsync();
        return selector;
    }

    public virtual async Task<IPage<Selector>> FindAll(IPageable pageable)
    {
        var page = await _selectorRepository.QueryHelper()
            .GetPageAsync(pageable);
        return page;
    }

    public virtual async Task<Selector> FindOne(long id)
    {
        var result = await _selectorRepository.QueryHelper()
            .GetOneAsync(selector => selector.Id == id);
        return result;
    }

    public virtual async Task Delete(long id)
    {
        await _selectorRepository.DeleteByIdAsync(id);
        await _selectorRepository.SaveChangesAsync();
    }
}
