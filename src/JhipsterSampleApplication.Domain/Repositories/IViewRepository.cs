using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Repositories
{
    public interface IViewRepository
    {
        Task<View?> GetByIdAsync(string id);
        Task<IEnumerable<View>> GetAllAsync();
        Task<View> AddAsync(View view);
        Task<View> UpdateAsync(View view);
        Task DeleteAsync(string id);
    }
} 