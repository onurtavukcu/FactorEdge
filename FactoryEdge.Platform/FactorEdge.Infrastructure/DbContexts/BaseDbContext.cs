using FactorEdge.Domain.Entities;
using FactorEdge.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.DbContexts
{
    public sealed class BaseDbContext(DbContextOptions<BaseDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new ApplicationUserConfiguration());

            modelBuilder.Entity<Tenant>(builder =>
            {
                builder.ToTable("Tenants");

                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();

                builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

                builder.Property(x => x.DatabaseName).IsRequired().HasMaxLength(150);

                builder.HasIndex(x => x.DatabaseName).IsUnique();

                builder.Property(x => x.Status).HasConversion<int>().IsRequired();

                builder.Property(x => x.ProvisioningError).HasMaxLength(2000);
            });
        }
    }
}
