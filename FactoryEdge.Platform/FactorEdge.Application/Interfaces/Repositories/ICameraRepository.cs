
using FactorEdge.Domain.Entities;

namespace FactorEdge.Application.Interfaces.Repositories;

public interface ICameraRepository
{
    Task<List<DeviceCamera>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<DeviceCamera?> GetByDeviceIdAsync(Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<DeviceCamera?> GetTrackedByDeviceIdAsync(Guid deviceId,
        CancellationToken cancellationToken = default);

    Task AddAsync(DeviceCamera camera, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
