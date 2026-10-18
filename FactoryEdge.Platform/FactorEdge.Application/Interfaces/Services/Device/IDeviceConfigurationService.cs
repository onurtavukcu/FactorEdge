using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Interfaces.Services
{
    public interface IDeviceConfigurationService
    {
        Task<List<DeviceConnectionDto>> GetActiveDevicesAsync(DeviceProtocol protocol, CancellationToken cancellationToken = default);
    }
}
