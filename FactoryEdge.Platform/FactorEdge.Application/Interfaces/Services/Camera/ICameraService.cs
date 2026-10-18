
using FactorEdge.Application.DTOs.Cameras;

namespace FactorEdge.Application.Interfaces.Services.Camera;

public interface ICameraService
{
    Task<CameraResponse> CreateAsync(Guid tenantId, CreateCameraRequest request, CancellationToken cancellationToken = default);

    Task<List<CameraResponse>> GetAllAsync(CancellationToken cancellationToken = default); 

    Task<CameraResponse?> GetByIdAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<CameraResponse?> UpdateAsync(Guid tenantId, Guid deviceId, UpdateCameraRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default);
}
