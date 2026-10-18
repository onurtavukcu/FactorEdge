
using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Camera;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;

namespace FactorEdge.Web.Background_Services;

public sealed class DeviceTelemetryBroadcastService(
    IServiceScopeFactory scopeFactory,
    IHubContext<DeviceTelemetryHub> hubContext,
    IConfiguration configuration,
    ILogger<DeviceTelemetryBroadcastService> logger)
    : BackgroundService
{
    private readonly Dictionary<Guid, DateTime> _lastDashboardBroadcastUtc = new();
    private const int DashboardBroadcastIntervalSeconds = 2;
    private readonly Dictionary<string, string> _lastStates = new();
    private readonly Dictionary<string, string> _lastCameraStates = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = Math.Max(1, configuration.GetValue<int>("DeviceMonitoring:BroadcastIntervalSeconds", 2));

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

        logger.LogInformation("Device telemetry broadcaster started.");

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CheckChangesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Device telemetry broadcast cycle failed.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task CheckChangesAsync(CancellationToken cancellationToken)
    {
        // Merkezi veritabanından aktif tenant'ları oku.
        using var platformScope = scopeFactory.CreateScope();

        var platformRepository = platformScope.ServiceProvider.GetRequiredService<IPlatformRepository>();

        var tenants = await platformRepository.GetActiveTenantsAsync(cancellationToken);

        var currentKeys = new HashSet<string>();
        var currentCameraKeys = new HashSet<string>();

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var scope = scopeFactory.CreateScope();

                // TenantDbContext kullanan servislerden ÖNCE
                // tenant bilgisini ayarlıyoruz.
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

                tenantContext.SetTenant(tenant.DatabaseName);

                var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

                var telemetryService = scope.ServiceProvider.GetRequiredService<IDeviceTelemetryService>();

                var eventLogService = scope.ServiceProvider.GetRequiredService<IDeviceEventLogService>();

                var devices = await deviceService.GetAllAsync(cancellationToken);

                var tenantGroup = DeviceTelemetryHub.TenantGroupName(tenant.Id);

                await hubContext.Clients.Group(tenantGroup).SendAsync(
                    "DeviceListUpdated",
                    devices.Select(device => new
                    {
                        deviceId = device.Id,
                        isActive = device.IsActive,
                        connectionStatus = device.ConnectionStatus.ToString(),
                        lastSeenUtc = device.LastSeenUtc
                    }).ToList(),
                    cancellationToken);

                foreach (var device in devices)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var group = DeviceTelemetryHub.GroupName(tenant.Id, device.Id);

                    currentKeys.Add(group);

                    try
                    {
                        var latestTelemetry = (await telemetryService.GetRecentAsync(device.Id, 1, cancellationToken)).FirstOrDefault();

                        var latestEvent = (await eventLogService.GetRecentAsync(device.Id, 1, cancellationToken)).FirstOrDefault();

                        // Son telemetri, son olay ve bağlantı durumu
                        // değişmediği sürece yayın yapmayacağız.
                        var state = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            TimestampUtc = latestTelemetry?.TimestampUtc,
                            MotorTemperature = latestTelemetry?.MotorTemperature,
                            MotorCurrent = latestTelemetry?.MotorCurrent,
                            ProductionCount = latestTelemetry?.ProductionCount,
                            ScrapCount = latestTelemetry?.ScrapCount,
                            Status = latestTelemetry?.Status,
                            LastEventId = latestEvent?.Id,
                            ConnectionStatus = device.ConnectionStatus,
                            IsActive = device.IsActive,
                            LastSeenUtc = device.LastSeenUtc
                        });

                        if (_lastStates.TryGetValue(group, out var previous) && previous == state)
                        {
                            continue;
                        }

                        _lastStates[group] = state;

                        var telemetries = await telemetryService.GetRecentAsync(device.Id, 50, cancellationToken);

                        var events = await eventLogService.GetRecentAsync(device.Id, 30, cancellationToken);

                        await hubContext.Clients.Group(group).SendAsync(
                            "DeviceUpdated",
                            new
                            {
                                DeviceId = device.Id,
                                Device = device,
                                ConnectionStatusName = device.ConnectionStatus.ToString(),
                                Telemetries = telemetries,
                                Events = events
                            },
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Broadcast failed for device {DeviceId}, tenant {TenantId}", device.Id, tenant.Id);
                    }
                }


                var cameraService = scope.ServiceProvider
                    .GetRequiredService<ICameraService>();

                var cameras = await cameraService.GetAllAsync(
                    cancellationToken);

                foreach (var camera in cameras)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cameraKey =
                        $"{tenant.Id:N}:{camera.DeviceId:N}";

                    currentCameraKeys.Add(cameraKey);

                    var state = System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            camera.OperatorStatus,
                            camera.ConnectionStatus,
                            camera.LastStatusAtUtc,
                            camera.IsActive
                        });

                    if (_lastCameraStates.TryGetValue(
                        cameraKey,
                        out var previousState) &&
                        previousState == state)
                    {
                        continue;
                    }

                    _lastCameraStates[cameraKey] = state;

                    await hubContext.Clients
                        .Group(tenantGroup)
                        .SendAsync(
                            "CameraStatusUpdated",
                            new
                            {
                                deviceId = camera.DeviceId,
                                operatorStatus = camera.OperatorStatus,
                                connectionStatus = camera.ConnectionStatus,
                                lastStatusAtUtc = camera.LastStatusAtUtc,
                                isActive = camera.IsActive
                            },
                            cancellationToken);

                    logger.LogDebug(
                        "Camera SignalR broadcast | Tenant: {TenantId} | Camera: {CameraId} | Status: {Status}",
                        tenant.Id,
                        camera.DeviceId,
                        camera.OperatorStatus);
                }


                var now = DateTime.UtcNow;

                if (!_lastDashboardBroadcastUtc.TryGetValue(tenant.Id, out var lastBroadcast) || now - lastBroadcast >= TimeSpan.FromSeconds(DashboardBroadcastIntervalSeconds))
                {
                    var dashboardRepository = scope.ServiceProvider.GetRequiredService<ITenantDashboardRepository>();

                    var totalTelemetry = await dashboardRepository.CountTelemetryAsync(cancellationToken);

                    var totalEvents = await dashboardRepository.CountEventLogsAsync(cancellationToken);

                    var efficiency = await dashboardRepository.GetEfficiencyLastHourAsync(cancellationToken);

                    await hubContext.Clients.Group(tenantGroup).SendAsync(
                        "DashboardUpdated",
                        new
                        {
                            totalTelemetryRecords = totalTelemetry,
                            totalEventLogs = totalEvents,
                            efficiency
                        },
                        cancellationToken);

                    _lastDashboardBroadcastUtc[tenant.Id] = now;
                }
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
                    "Broadcast failed for tenant {TenantId}",
                    tenant.Id);
            }
        }

        // Silinmiş/pasifleştirilmiş tenant ve cihazların
        // eski durumlarını bellekte tutmayalım.
        foreach (var key in _lastStates.Keys.Where(x => !currentKeys.Contains(x)).ToList())
        {
            _lastStates.Remove(key);
        }

        foreach (var key in _lastCameraStates.Keys.Where(x => !currentCameraKeys.Contains(x)).ToList())
        {
            _lastCameraStates.Remove(key);
        }
    }
}
