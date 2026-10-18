using FactorEdge.Application.DTOs.Cameras;

namespace FactorEdge.Application.Interfaces.Services.Camera
{
    public interface ICameraProvisioningService
    {
        Task<CameraProvisionResult> CreateAsync(Guid tenantId, Guid deviceId, string streamUrl, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default);
    }
}
