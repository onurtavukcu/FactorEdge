using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Tenants.Models
{
    public sealed class TenantListDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public TenantStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
