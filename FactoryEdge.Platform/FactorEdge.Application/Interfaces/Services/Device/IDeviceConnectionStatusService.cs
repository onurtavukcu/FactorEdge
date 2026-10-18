namespace FactorEdge.Application.Interfaces.Services.Device
{
    public interface IDeviceConnectionStatusService
    {
        Task<int> MarkStaleDevicesAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    }
}
