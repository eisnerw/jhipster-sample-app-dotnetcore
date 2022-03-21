using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jhipster.Domain.Repositories.Interfaces
{
    public interface ICategoryRepository : IGenericRepository<Category>
    {
        Task<string> Analyze(IList<string> ids);        
    }
}
