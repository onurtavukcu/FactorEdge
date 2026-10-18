
using FactorEdge.Domain.Common;

namespace FactorEdge.Domain.Entities;

public sealed class CameraStatusHistory : BaseEntity<Guid>
{
    public Guid DeviceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public Device Device { get; set; } = null!;
}
