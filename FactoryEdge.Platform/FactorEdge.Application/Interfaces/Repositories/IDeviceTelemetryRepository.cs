using FactorEdge.Domain.Entities;

namespace FactorEdge.Application.Interfaces.Repositories
{
    public interface IDeviceTelemetryRepository
    {
        Task AddAsync(DeviceTelemetry telemetry, CancellationToken cancellationToken = default);
        Task<List<DeviceTelemetry>> GetRecentByDeviceIdAsync(Guid deviceId, int take = 50, CancellationToken cancellationToken = default);
        Task<List<DeviceTelemetry>> GetHistoryAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
