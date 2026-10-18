namespace FactorEdge.Application.DTOs.Cameras;

public sealed class CameraResponse
{
    public Guid DeviceId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? ContainerName { get; set; }
    public string OperatorStatus { get; set; } = string.Empty;
    public string ConnectionStatus { get; set; } = string.Empty;
    public DateTime? LastStatusAtUtc { get; set; }
    public bool IsActive { get; set; }
}