using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IViewService
    {
        Task<ViewDto> GetByIdAsync(string id);
        Task<IEnumerable<ViewDto>> GetAllAsync();
        Task<ViewDto> CreateAsync(ViewDto viewDto);
        Task<ViewDto> UpdateAsync(ViewDto viewDto);
        Task DeleteAsync(string id);
    }
} 