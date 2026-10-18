using FactorEdge.Application.Interfaces;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Repositories
{
    public sealed class DeviceRepository(TenantDbContext dbContext) : IDeviceRepository
    {
        public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id && !x.IsDeleted,
                    cancellationToken);
        }

        public async Task<Device?> GetByCodeAsync(string deviceCode, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.DeviceCode == deviceCode && !x.IsDeleted,
                    cancellationToken);
        }

        public async Task<List<Device>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
        {
            await dbContext.Devices.AddAsync(device, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        public async Task<Device?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        }

        public void HardDelete(Device device)
        {
            dbContext.Devices.Remove(device);
        }

        public async Task<Device?> GetTrackedByCodeAsync(string deviceCode, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .FirstOrDefaultAsync(
                    x => x.DeviceCode == deviceCode && !x.IsDeleted,
                    cancellationToken);
        }

        public async Task<List<Device>> GetActiveByProtocolAsync(DeviceProtocol protocol, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .AsNoTracking()
                .Where(x =>
                    x.DeviceType == DeviceType.ProductionLine &&
                    x.Protocol == protocol &&
                    x.IsActive &&
                    !x.IsDeleted)
                .OrderBy(x => x.DeviceCode)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> MarkStaleDevicesDisconnectedAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .Where(x =>
                    x.DeviceType == DeviceType.ProductionLine &&
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.ConnectionStatus == DeviceConnectionStatus.Connected &&
                    (x.LastSeenUtc == null ||
                     x.LastSeenUtc < cutoffUtc))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.ConnectionStatus,
                            DeviceConnectionStatus.Disconnected),
                    cancellationToken);
        }
        public async Task<List<Device>> GetStaleConnectedDevicesAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .Where(x =>
                    x.DeviceType == DeviceType.ProductionLine &&
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.ConnectionStatus == DeviceConnectionStatus.Connected &&
                    (x.LastSeenUtc == null ||
                     x.LastSeenUtc < cutoffUtc))
                .ToListAsync(cancellationToken);
        }

        public async Task<int> MarkDisconnectedIfStaleAsync(Guid deviceId, DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            return await dbContext.Devices
                .Where(x =>
                    x.Id == deviceId &&
                    x.DeviceType == DeviceType.ProductionLine &&
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.ConnectionStatus == DeviceConnectionStatus.Connected &&
                    (x.LastSeenUtc == null ||
                     x.LastSeenUtc < cutoffUtc))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.ConnectionStatus,
                        DeviceConnectionStatus.Disconnected),
                    cancellationToken);
        }

        public async Task<int> MarkDisconnectedWithEventIfStaleAsync(Guid deviceId, DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Cihaz hâlâ stale ve Connected ise güncelle.
            int affectedRows = await dbContext.Devices
                .Where(x =>
                    x.Id == deviceId &&
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.ConnectionStatus == DeviceConnectionStatus.Connected &&
                    x.DeviceType == DeviceType.ProductionLine &&
                    (x.LastSeenUtc == null ||
                     x.LastSeenUtc < cutoffUtc))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.ConnectionStatus,
                        DeviceConnectionStatus.Disconnected),
                    cancellationToken);

            if (affectedRows == 0)
            {
                // Bu sırada yeni telemetry gelmiş olabilir.
                await transaction.RollbackAsync(cancellationToken);
                return 0;
            }

            var eventLog = new DeviceEventLog
            {
                DeviceId = deviceId,
                EventType = DeviceEventType.ConnectionChanged,
                OldValue = DeviceConnectionStatus.Connected.ToString(),
                NewValue = DeviceConnectionStatus.Disconnected.ToString(),
                Description = "Belirlenen süre boyunca cihazdan veri alınamadı.",
                OccurredAtUtc = DateTime.UtcNow
            };

            dbContext.DeviceEventLogs.Add(eventLog);

            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return affectedRows;
        }

        public async Task<Device?> GetTrackedByCodeForUpdateAsync(string deviceCode, CancellationToken cancellationToken = default)
        {
            var devices = await dbContext.Devices
                .FromSqlInterpolated($"""
                        SELECT *
                        FROM "Devices"
                        WHERE "DeviceCode" = {deviceCode}
                          AND "IsDeleted" = FALSE
                          AND "DeviceType" = 1
                        FOR UPDATE
                        """)
                .AsTracking()
                .ToListAsync(cancellationToken);

            return devices.SingleOrDefault();
        }

        public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            return new AppTransaction(transaction);
        }
    }
}
