namespace FactorEdge.Application.DTOs.Cameras;

public sealed class UpdateCameraRequest
{
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}