using FactorEdge.Domain.Common;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Domain.Entities
{
    public class Tenant : BaseEntity<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public TenantStatus Status { get; set; } = TenantStatus.Provisioning;
        public string? ProvisioningError { get; set; }
    }
}
