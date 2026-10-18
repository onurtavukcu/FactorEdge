
using FactorEdge.Application.DTOs.Cameras;

namespace FactorEdge.Application.Interfaces.Services.Camera;

public interface ICameraStatusHistoryService
{
    Task<List<CameraStatusHistoryResponse>> GetRecentAsync(Guid tenantId, int count = 50, CancellationToken cancellationToken = default);
}
 