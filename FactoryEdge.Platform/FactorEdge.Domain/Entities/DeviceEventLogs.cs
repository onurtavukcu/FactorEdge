using FactorEdge.Domain.Common;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Domain.Entities
{
    public sealed class DeviceEventLog : BaseEntity<long>
    {
        public Guid DeviceId { get; set; }
        public Device Device { get; set; } = null!;
        public DeviceEventType EventType { get; set; }
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    }
}
