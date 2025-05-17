using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using JhipsterSampleApplication.Infrastructure.Data;

namespace JhipsterSampleApplication.Infrastructure.Data.Repositories
{
    public class ViewRepository : IViewRepository
    {
        private readonly ApplicationDatabaseContext _context;

        public ViewRepository(ApplicationDatabaseContext context)
        {
            _context = context;
        }

        public async Task<View> GetByIdAsync(string id)
        {
            return await _context.Views
                .Include(v => v.PrimaryView)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<IEnumerable<View>> GetAllAsync()
        {
            return await _context.Views
                .Include(v => v.PrimaryView)
                .ToListAsync();
        }

        public async Task<View> AddAsync(View view)
        {
            await _context.Views.AddAsync(view);
            await _context.SaveChangesAsync();
            return view;
        }

        public async Task<View> UpdateAsync(View view)
        {
            _context.Views.Update(view);
            await _context.SaveChangesAsync();
            return view;
        }

        public async Task DeleteAsync(string id)
        {
            var view = await GetByIdAsync(id);
            if (view != null)
            {
                _context.Views.Remove(view);
                await _context.SaveChangesAsync();
            }
        }
    }
} 