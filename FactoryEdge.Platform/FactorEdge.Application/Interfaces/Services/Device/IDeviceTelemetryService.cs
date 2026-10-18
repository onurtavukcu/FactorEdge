using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Contracts.Models;

namespace FactorEdge.Application.Interfaces.Services.Device
{

    public interface IDeviceTelemetryService
    {
        Task<List<DeviceTelemetryItemDto>> GetRecentAsync(Guid deviceId, int take = 50, CancellationToken cancellationToken = default);
        Task<List<DeviceTelemetryItemDto>> GetHistoryAsync(Guid deviceId, CancellationToken cancellationToken = default);
        Task SaveAsync(ProductionLineTelemetry telemetry, CancellationToken cancellationToken = default);
    }
}
