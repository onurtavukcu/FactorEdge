
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.MqttWorker.Services;

public sealed class CameraOfflineMonitoringService(
    IServiceScopeFactory scopeFactory,
    ILogger<CameraOfflineMonitoringService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan OfflineThreshold =
        TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Camera offline monitoring started.");

        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CheckCamerasAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Camera offline monitoring cycle failed.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }
    }

    private async Task CheckCamerasAsync(
        CancellationToken cancellationToken)
    {
        List<(Guid TenantId, string DatabaseName)> tenants;

        // Aktif tenant'ları merkezi veritabanından al.
        using (var scope = scopeFactory.CreateScope())
        {
            var baseDb = scope.ServiceProvider
                .GetRequiredService<BaseDbContext>();

            var activeTenants = await baseDb.Tenants
                .AsNoTracking()
                .Where(x => x.Status == TenantStatus.Active)
                .Select(x => new
                {
                    x.Id,
                    x.DatabaseName
                })
                .ToListAsync(cancellationToken);

            tenants = activeTenants
                .Select(x => (x.Id, x.DatabaseName))
                .ToList();
        }

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var scope = scopeFactory.CreateScope();

                var tenantContext = scope.ServiceProvider
                    .GetRequiredService<ITenantContext>();

                tenantContext.SetTenant(tenant.DatabaseName);

                var db = scope.ServiceProvider
                    .GetRequiredService<TenantDbContext>();

                var now = DateTime.UtcNow;
                var cutoff = now - OfflineThreshold;

                // Yalnızca süresi dolmuş ve henüz Offline
                // olmayan aktif kameraları bul.
                var cameras = await db.DeviceCameras
                    .Include(x => x.Device)
                    .Where(x =>
                        x.Device.DeviceType == DeviceType.Camera &&
                        x.Device.IsActive &&
                        !x.Device.IsDeleted &&
                        x.LastStatusAtUtc != null &&
                        x.LastStatusAtUtc < cutoff &&
                        x.LastOperatorStatus != "Offline")
                    .ToListAsync(cancellationToken);

                if (cameras.Count == 0)
                    continue;

                foreach (var camera in cameras)
                {
                    camera.LastOperatorStatus = "Offline";

                    camera.Device.ConnectionStatus =
                        DeviceConnectionStatus.Disconnected;

                    // LastStatusAtUtc değiştirmiyoruz.
                    // Son başarılı MQTT mesajının zamanı kalacak.

                    db.CameraStatusHistories.Add(
                        new CameraStatusHistory
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = camera.DeviceId,
                            Status = "Offline",
                            CreatedAtUtc = now
                        });

                    logger.LogWarning(
                        "CAMERA OFFLINE | Tenant: {TenantId} | Camera: {DeviceId}",
                        tenant.TenantId,
                        camera.DeviceId);
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Camera offline check failed for tenant {TenantId}",
                    tenant.TenantId);
            }
        }
    }
}
