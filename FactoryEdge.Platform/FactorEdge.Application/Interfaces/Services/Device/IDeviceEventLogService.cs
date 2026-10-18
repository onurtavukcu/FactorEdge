using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Interfaces.Services.Device
{
    public interface IDeviceEventLogService
    {
        Task AddAsync(Guid deviceId, DeviceEventType eventType, string? oldValue, string newValue, string description, CancellationToken cancellationToken = default);
        Task<List<DeviceEventLogDto>> GetRecentAsync(Guid deviceId, int take = 30, CancellationToken cancellationToken = default);
    }
}
