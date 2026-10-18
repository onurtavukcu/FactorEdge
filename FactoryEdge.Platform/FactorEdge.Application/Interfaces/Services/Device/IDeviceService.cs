using FactorEdge.Application.DTOs.Devices;

namespace FactorEdge.Application.Interfaces.Services.Device
{
    public interface IDeviceService
    {
        Task<Guid> CreateAsync(Guid tenantId, CreateDeviceRequest request, CancellationToken cancellationToken = default);
        Task<List<DeviceResponse>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<UpdateDeviceRequest?> GetForEditAsync(Guid id, CancellationToken cancellationToken = default);
        Task UpdateAsync(UpdateDeviceRequest request, CancellationToken cancellationToken = default);
        Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task SoftDeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    }
}
