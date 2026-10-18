using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Tenants;
using FactorEdge.Infrastructure.Persistence.DbContextFactory;
using FactorEdge.Infrastructure.Persistence.Seeder;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Repositories
{
    public sealed class TenantProvisioningRepository(ITenantDatabaseProvisioner databaseProvisioner, TenantDbContextFactory dbContextFactory, TenantAdminSeeder tenantAdminSeeder) : ITenantProvisioningRepository
    {
        public Task<bool> CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default) 
        {
            return databaseProvisioner.CreateDatabaseAsync(databaseName, cancellationToken); 
        }

        public async Task MigrateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default) 
        {
            await using var tenantDbContext = dbContextFactory.Create(databaseName);

            await tenantDbContext.Database.MigrateAsync(cancellationToken);
        }

        public Task SeedTenantAdminAsync(Guid tenantId, string userName, string email, string password, CancellationToken cancellationToken = default)
        {
            return tenantAdminSeeder.SeedAsync(tenantId, userName, email, password, cancellationToken);
        }

        public Task DeleteDatabaseAsync(string databaseName, CancellationToken cancellationToken = default)
        {
            return databaseProvisioner.DeleteDatabaseAsync(databaseName, cancellationToken);
        }
    }
}
