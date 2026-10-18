using FactorEdge.Application.Interfaces.Tenants;

namespace FactorEdge.Application.Features.Tenants.Services
{
    public sealed class TenantContext : ITenantContext
    {
        public string? DatabaseName { get; private set; }

        public void SetTenant(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException(
                    "Tenant database name is required.");

            if (DatabaseName is not null)
                throw new InvalidOperationException(
                    "Tenant has already been selected for this scope.");

            DatabaseName = databaseName;
        }
    }
}
