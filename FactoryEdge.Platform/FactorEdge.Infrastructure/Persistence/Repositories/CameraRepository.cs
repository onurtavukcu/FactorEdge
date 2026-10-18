
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Repositories;

public sealed class CameraRepository(TenantDbContext dbContext)
    : ICameraRepository
{
    public async Task<List<DeviceCamera>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DeviceCameras
            .AsNoTracking()
            .Include(x => x.Device)
            .Where(x =>
                x.Device.DeviceType == DeviceType.Camera &&
                !x.Device.IsDeleted)
            .OrderBy(x => x.Device.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceCamera?> GetByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DeviceCameras
            .AsNoTracking()
            .Include(x => x.Device)
            .FirstOrDefaultAsync(
                x => x.DeviceId == deviceId &&
                     x.Device.DeviceType == DeviceType.Camera &&
                     !x.Device.IsDeleted,
                cancellationToken);
    }

    public async Task<DeviceCamera?> GetTrackedByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.DeviceCameras
            .Include(x => x.Device)
            .FirstOrDefaultAsync(
                x => x.DeviceId == deviceId &&
                     x.Device.DeviceType == DeviceType.Camera &&
                     !x.Device.IsDeleted,
                cancellationToken);
    }

    public async Task AddAsync(
        DeviceCamera camera,
        CancellationToken cancellationToken = default)
    {
        await dbContext.DeviceCameras.AddAsync(
            camera,
            cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
