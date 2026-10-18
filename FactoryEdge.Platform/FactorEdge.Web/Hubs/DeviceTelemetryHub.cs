
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Application.Interfaces.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace FactorEdge.Web.Hubs;

[Authorize(Roles = "TenantAdmin,RegularUser")]
public sealed class DeviceTelemetryHub(IServiceScopeFactory scopeFactory) : Hub
{
    public static string GroupName(Guid tenantId, Guid deviceId) => $"tenant:{tenantId:N}:device:{deviceId:N}";

    public async Task JoinDevice(Guid deviceId)
    {
        var tenantIdValue = Context.User?.FindFirstValue("tenantId");

        if (!Guid.TryParse(tenantIdValue, out var tenantId) || tenantId == Guid.Empty || deviceId == Guid.Empty)
        {
            throw new HubException("Geçersiz tenant veya cihaz.");
        }

        using var scope = scopeFactory.CreateScope();

        var platformRepository = scope.ServiceProvider.GetRequiredService<IPlatformRepository>();

        var databaseName = await platformRepository.GetActiveTenantDatabaseNameAsync(tenantId, Context.ConnectionAborted);

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new HubException("Tenant aktif değil.");

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.SetTenant(databaseName);

        // Tenant belirlendikten sonra resolve ediyoruz.
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        var telemetryService = scope.ServiceProvider.GetRequiredService<IDeviceTelemetryService>();

        var eventLogService = scope.ServiceProvider.GetRequiredService<IDeviceEventLogService>();

        var devices = await deviceService.GetAllAsync(Context.ConnectionAborted);

        var device = devices.FirstOrDefault(x => x.Id == deviceId);

        if (device is null)
            throw new HubException("Cihaz bulunamadı.");

        var telemetries = await telemetryService.GetRecentAsync(deviceId, 50, Context.ConnectionAborted);

        var events = await eventLogService.GetRecentAsync(deviceId, 30, Context.ConnectionAborted);

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId, deviceId), Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
            "DeviceUpdated",
            new
            {
                DeviceId = deviceId,
                Device = device,
                ConnectionStatusName = device.ConnectionStatus.ToString(),
                Telemetries = telemetries,
                Events = events
            },
            Context.ConnectionAborted);
    }

    public async Task LeaveDevice(Guid deviceId)
    {
        var tenantIdValue = Context.User?.FindFirstValue("tenantId");

        if (!Guid.TryParse(tenantIdValue, out var tenantId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tenantId, deviceId), Context.ConnectionAborted);
    }

    public static string TenantGroupName(Guid tenantId) => $"tenant:{tenantId:N}:dashboard";

    public async Task JoinTenant()
    {
        var tenantIdValue = Context.User?.FindFirstValue("tenantId");

        if (!Guid.TryParse(tenantIdValue, out var tenantId) || tenantId == Guid.Empty)
        {
            throw new HubException("Geçersiz tenant.");
        }

        using var scope = scopeFactory.CreateScope();

        var platformRepository = scope.ServiceProvider.GetRequiredService<IPlatformRepository>();

        var databaseName = await platformRepository.GetActiveTenantDatabaseNameAsync(tenantId, Context.ConnectionAborted);

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new HubException("Tenant aktif değil.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            TenantGroupName(tenantId),
            Context.ConnectionAborted);
    }

    public async Task LeaveTenant()
    {
        var tenantIdValue = Context.User?.FindFirstValue("tenantId");

        if (!Guid.TryParse(tenantIdValue, out var tenantId) || tenantId == Guid.Empty)
            return;

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            TenantGroupName(tenantId),
            Context.ConnectionAborted);
    }
}
