using Microsoft.EntityFrameworkCore;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Infrastructure.Persistence
{
    public class ApplicationDatabaseContext : DbContext
    {
        public DbSet<View> Views { get; set; }
    }
} 