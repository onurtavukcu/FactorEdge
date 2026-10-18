namespace FactorEdge.Application.DTOs.Cameras;

public sealed class CreateCameraRequest
{
    public string DeviceCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
}