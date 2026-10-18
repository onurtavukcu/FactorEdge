using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.DTOs.Devices;

public sealed class DeviceEventLogDto
{
    public long Id { get; set; }
    public DeviceEventType EventType { get; set; }
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string EventTypeName => EventType.ToString();
}