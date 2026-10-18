using FactorEdge.Domain.Common;

namespace FactorEdge.Domain.Entities
{
    public class DeviceCamera : BaseEntity<Guid>
    {
        public Guid DeviceId { get; set; }
        public string StreamUrl { get; set; } = string.Empty;
        public string? ContainerName { get; set; }
        public string LastOperatorStatus { get; set; } = "Unknown";
        public DateTime? LastStatusAtUtc { get; set; }
        public Device Device { get; set; } = null!;
    }
}
