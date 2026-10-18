using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Tenants.Models
{
    public sealed class UpdateTenantRequest
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public TenantStatus Status { get; set; }
        public string AdminUserName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public bool AdminIsActive { get; set; }
    }
}
