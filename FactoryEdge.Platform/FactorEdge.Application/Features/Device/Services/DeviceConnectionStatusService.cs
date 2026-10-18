using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Device;

namespace FactorEdge.Application.Features.Device.Services;

public sealed class DeviceConnectionStatusService(IDeviceRepository deviceRepository) : IDeviceConnectionStatusService
{
    public async Task<int> MarkStaleDevicesAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        DateTime cutoffUtc = DateTime.UtcNow - timeout;

        var devices = await deviceRepository
            .GetStaleConnectedDevicesAsync(
                cutoffUtc,
                cancellationToken);

        int affectedRows = 0;

        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            affectedRows += await deviceRepository
                .MarkDisconnectedWithEventIfStaleAsync(
                    device.Id,
                    cutoffUtc,
                    cancellationToken);
        }

        return affectedRows;
    }
}