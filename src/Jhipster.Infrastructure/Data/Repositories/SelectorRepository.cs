using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using Jhipster.Domain;
using Jhipster.Domain.Repositories.Interfaces;
using Jhipster.Infrastructure.Data.Extensions;

namespace Jhipster.Infrastructure.Data.Repositories
{
    public class SelectorRepository : GenericRepository<Selector>, ISelectorRepository
    {
        public SelectorRepository(IUnitOfWork context) : base(context)
        {
        }

        public override async Task<Selector> CreateOrUpdateAsync(Selector selector)
        {
            bool exists = await Exists(x => x.Id == selector.Id);

            if (selector.Id != 0 && exists)
            {
                Update(selector);
            }
            else
            {
                _context.AddOrUpdateGraph(selector);
            }
            return selector;
        }
    }
}
