using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Device.Services
{
    public sealed class DeviceEventLogService(IDeviceEventLogRepository eventLogRepository) : IDeviceEventLogService
    {
        public async Task AddAsync(Guid deviceId, DeviceEventType eventType, string? oldValue, string newValue, string description, CancellationToken cancellationToken = default)
        {
            if (oldValue == newValue)
                return;

            var eventLog = new DeviceEventLog
            {
                DeviceId = deviceId,
                EventType = eventType,
                OldValue = oldValue,
                NewValue = newValue,
                Description = description,
                OccurredAtUtc = DateTime.UtcNow
            };

            await eventLogRepository.AddAsync(eventLog, cancellationToken);
        }

        public async Task<List<DeviceEventLogDto>> GetRecentAsync(Guid deviceId, int take = 30, CancellationToken cancellationToken = default)
        {
            var events = await eventLogRepository.GetRecentByDeviceIdAsync(deviceId, take, cancellationToken);

            return events.Select(x => new DeviceEventLogDto
            {
                Id = x.Id,
                EventType = x.EventType,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                Description = x.Description,
                OccurredAtUtc = x.OccurredAtUtc
            }).ToList();
        }
    }
}
