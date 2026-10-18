namespace FactorEdge.Application.Interfaces.Tenants
{
    public interface ITenantContext
    {
        string? DatabaseName { get; }
        void SetTenant(string databaseName);
    }
}
