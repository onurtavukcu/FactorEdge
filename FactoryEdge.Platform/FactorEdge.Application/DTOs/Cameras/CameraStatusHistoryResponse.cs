
namespace FactorEdge.Application.DTOs.Cameras;

public sealed class CameraStatusHistoryResponse
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string CameraName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
