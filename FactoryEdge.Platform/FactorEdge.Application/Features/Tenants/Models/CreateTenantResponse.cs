namespace FactorEdge.Application.Features.Tenants.Models
{
    public sealed class CreateTenantResponse
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
