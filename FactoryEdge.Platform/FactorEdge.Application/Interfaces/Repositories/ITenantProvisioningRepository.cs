namespace FactorEdge.Application.Interfaces.Repositories
{
    public interface ITenantProvisioningRepository 
    {
        Task<bool> CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
        Task MigrateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
        Task SeedTenantAdminAsync(Guid tenantId, string userName, string email, string password, CancellationToken cancellationToken = default);
        Task DeleteDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
    }
}
