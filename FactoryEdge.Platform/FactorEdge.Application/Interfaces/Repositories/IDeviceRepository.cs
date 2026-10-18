using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Interfaces.Repositories
{
    public interface IDeviceRepository
    {
        Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Device?> GetByCodeAsync(string deviceCode, CancellationToken cancellationToken = default);
        Task<List<Device>> GetAllAsync(CancellationToken cancellationToken = default);
        Task AddAsync(Device device, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);
        void HardDelete(Device device);
        Task<Device?> GetTrackedByCodeAsync(string deviceCode, CancellationToken cancellationToken = default);
        Task<List<Device>> GetActiveByProtocolAsync(DeviceProtocol protocol, CancellationToken cancellationToken = default);
        Task<int> MarkStaleDevicesDisconnectedAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
        Task<List<Device>> GetStaleConnectedDevicesAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
        Task<int> MarkDisconnectedIfStaleAsync(Guid deviceId, DateTime cutoffUtc, CancellationToken cancellationToken = default);
        Task<int> MarkDisconnectedWithEventIfStaleAsync(Guid deviceId, DateTime cutoffUtc, CancellationToken cancellationToken = default);
        Task<Device?> GetTrackedByCodeForUpdateAsync(string deviceCode, CancellationToken cancellationToken = default);
        Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
