using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Domain.Entities;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Repositories
{
    public sealed class DeviceTelemetryRepository(TenantDbContext dbContext) : IDeviceTelemetryRepository
    {
        public async Task AddAsync(DeviceTelemetry telemetry,CancellationToken cancellationToken = default)
        {
            await dbContext.DeviceTelemetries.AddAsync(telemetry, cancellationToken);
        }

        public Task<List<DeviceTelemetry>> GetRecentByDeviceIdAsync(Guid deviceId, int take = 50, CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 200);

            return dbContext.DeviceTelemetries
                .AsNoTracking()
                .Where(x => x.DeviceId == deviceId)
                .OrderByDescending(x => x.TimestampUtc)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        public Task<List<DeviceTelemetry>> GetHistoryAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        {
            return dbContext.DeviceTelemetries
                .AsNoTracking()
                .Where(x =>
                    x.DeviceId == deviceId &&
                    x.TimestampUtc >= fromUtc &&
                    x.TimestampUtc <= toUtc)
                .OrderBy(x => x.TimestampUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

    }
}
