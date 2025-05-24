using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using JhipsterSampleApplication.Infrastructure.Data.Extensions;

namespace JhipsterSampleApplication.Infrastructure.Data.Repositories
{
    public class NamedQueryRepository : GenericRepository<NamedQuery, long>, INamedQueryRepository
    {
        public NamedQueryRepository(IUnitOfWork context) : base(context)
        {
        }

        public async Task<IPage<NamedQuery>> FindAllNamedQueries(IPageable pageable)
        {
            if (pageable == null)
            {
                var allItems = await _dbSet.AsNoTracking().ToListAsync();
                var defaultPageable = Pageable.Of(0, Math.Max(1, allItems.Count));
                return new Page<NamedQuery>(allItems, defaultPageable, allItems.Count);
            }
            return await QueryHelper().GetPageAsync(pageable);
        }

        public async Task<NamedQuery?> FindOneByName(string name)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(nq => nq.Name == name);
        }

        public async Task<List<NamedQuery>> FindByOwnerAsync(string owner)
        {
            return await _dbSet.AsNoTracking()
                .Where(nq => nq.Owner == owner)
                .ToListAsync();
        }

        public async Task<NamedQuery?> FindOne(long id)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(nq => nq.Id == id);
        }

        public override async Task<NamedQuery> CreateOrUpdateAsync(NamedQuery namedQuery)
        {
            List<Type> entitiesToBeUpdated = new List<Type>();
            return await base.CreateOrUpdateAsync(namedQuery, entitiesToBeUpdated);
        }
    }
} 