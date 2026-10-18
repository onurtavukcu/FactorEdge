using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Device.Services
{
    public sealed class DeviceConfigurationService(IDeviceRepository deviceRepository) : IDeviceConfigurationService
    {
        public async Task<List<DeviceConnectionDto>> GetActiveDevicesAsync(DeviceProtocol protocol, CancellationToken cancellationToken = default)
        {
            var devices = await deviceRepository.GetActiveByProtocolAsync(protocol, cancellationToken);

            return devices.Select(x => new DeviceConnectionDto
            {
                Id = x.Id,
                DeviceCode = x.DeviceCode,
                Protocol = x.Protocol,
                Host = x.Host,
                Port = x.Port,
                UnitId = x.UnitId,
                RegisterMapJson = x.RegisterMapJson,
                Topic = x.Topic
            }).ToList();
        }
    }
}
