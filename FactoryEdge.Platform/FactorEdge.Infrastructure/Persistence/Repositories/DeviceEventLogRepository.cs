using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Domain.Entities;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Repositories
{
    public sealed class DeviceEventLogRepository(TenantDbContext dbContext) : IDeviceEventLogRepository
    {
        public async Task AddAsync(DeviceEventLog eventLog, CancellationToken cancellationToken = default)
        {
            await dbContext.DeviceEventLogs.AddAsync(eventLog, cancellationToken);
        }

        public Task<List<DeviceEventLog>> GetRecentByDeviceIdAsync(Guid deviceId, int take = 30, CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 100);

            return dbContext.DeviceEventLogs
                .AsNoTracking()
                .Where(x => x.DeviceId == deviceId)
                .OrderByDescending(x => x.OccurredAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
    }
}
