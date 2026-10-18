using FactorEdge.Domain.Entities;

namespace FactorEdge.Application.Interfaces.Repositories
{

    public interface IDeviceEventLogRepository
    {
        Task AddAsync(DeviceEventLog eventLog, CancellationToken cancellationToken = default);
        Task<List<DeviceEventLog>> GetRecentByDeviceIdAsync(Guid deviceId, int take = 30, CancellationToken cancellationToken = default);
    }
}
