namespace FactorEdge.Application.Interfaces.Services.Tenants
{
    public interface ITenantDatabaseProvisioner
    {
        Task<bool> CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
        Task DeleteDatabaseAsync(string databaseName, CancellationToken cancellationToken = default);
    }
}
