namespace FactorEdge.Application.Features.Tenants.Models
{
    public sealed class CreateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string AdminUserName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }
}
