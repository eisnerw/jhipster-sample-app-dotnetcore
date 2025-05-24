using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Entities.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JhipsterSampleApplication.Infrastructure.Data
{
    public class ApplicationDatabaseContext(DbContextOptions<ApplicationDatabaseContext> options, IHttpContextAccessor httpContextAccessor) : IdentityDbContext<
        User, Role, string,
        IdentityUserClaim<string>,
        UserRole,
        IdentityUserLogin<string>,
        IdentityRoleClaim<string>,
        IdentityUserToken<string>
    >(options)
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        public required DbSet<Country> Countries { get; set; }
        public required DbSet<Department> Departments { get; set; }
        public required DbSet<Employee> Employees { get; set; }
        public required DbSet<Job> Jobs { get; set; }
        public required DbSet<JobHistory> JobHistories { get; set; }
        public required DbSet<Location> Locations { get; set; }
        public required DbSet<PieceOfWork> PieceOfWorks { get; set; }
        public required DbSet<Region> Regions { get; set; }
        public required DbSet<TimeSheet> TimeSheets { get; set; }
        public required DbSet<TimeSheetEntry> TimeSheetEntries { get; set; }
        public required DbSet<Birthday> Birthdays { get; set; }
        public required DbSet<View> Views { get; set; }
        public required DbSet<NamedQuery> NamedQueries { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Rename AspNet default tables
            builder.Entity<User>().ToTable("Users");
            builder.Entity<Role>().ToTable("Roles");
            builder.Entity<UserRole>().ToTable("UserRoles");
            builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
            builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
            builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
            builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

            builder.Entity<UserRole>(userRole =>
            {
                userRole.HasKey(ur => new { ur.UserId, ur.RoleId });

                userRole.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId)
                    .IsRequired();

                userRole.HasOne(ur => ur.User)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.UserId)
                    .IsRequired();
            });

            builder.Entity<User>()
                .HasMany(e => e.UserRoles)
                .WithOne()
                .HasForeignKey(e => e.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Job>()
                .HasMany(x => x.Chores)
                .WithMany(x => x.Jobs)
                .UsingEntity<Dictionary<string, object>>(
                    "JobChores",
                    x => x.HasOne<PieceOfWork>().WithMany(),
                    x => x.HasOne<Job>().WithMany());

            builder.Entity<Birthday>(entity =>
            {
                entity.ToTable("birthday");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Text).IsRequired();
                entity.Property(e => e.Lname).IsRequired();
                entity.Property(e => e.Fname).IsRequired();
                entity.Property(e => e.Sign).IsRequired();
                entity.Property(e => e.Dob).IsRequired();
                entity.Property(e => e.IsAlive).IsRequired();
                entity.Property(e => e.Wikipedia).IsRequired();
                entity.Property(e => e.Categories)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
            });

            builder.Entity<View>(entity =>
            {
                entity.ToTable("view");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Query).IsRequired();
                entity.Property(e => e.CategoryQuery).IsRequired(false);
            });

            builder.Entity<NamedQuery>(entity =>
            {
                entity.ToTable("named_query");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Text).IsRequired();
                entity.Property(e => e.Owner).IsRequired().HasMaxLength(50);
            });
        }

        /// <summary>
        /// SaveChangesAsync with entities audit
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries = ChangeTracker
              .Entries()
              .Where(e => e.Entity is IAuditedEntityBase && (
                  e.State == EntityState.Added
                  || e.State == EntityState.Modified));

            string modifiedOrCreatedBy = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "System";

            foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entityEntry in entries)
            {
                if (entityEntry.State == EntityState.Added)
                {
                    ((IAuditedEntityBase)entityEntry.Entity).CreatedDate = DateTime.Now;
                    ((IAuditedEntityBase)entityEntry.Entity).CreatedBy = modifiedOrCreatedBy;
                }
                else
                {
                    Entry((IAuditedEntityBase)entityEntry.Entity).Property(p => p.CreatedDate).IsModified = false;
                    Entry((IAuditedEntityBase)entityEntry.Entity).Property(p => p.CreatedBy).IsModified = false;
                }
              ((IAuditedEntityBase)entityEntry.Entity).LastModifiedDate = DateTime.Now;
                ((IAuditedEntityBase)entityEntry.Entity).LastModifiedBy = modifiedOrCreatedBy;
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
