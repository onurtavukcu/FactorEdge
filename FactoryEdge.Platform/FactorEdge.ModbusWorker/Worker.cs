using FactorEdge.Application.Interfaces.Services;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using FactorEdge.ModbusWorker.Services;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.ModbusWorker;

public sealed class Worker(ILogger<Worker> logger, ModbusReader reader, IConfiguration configuration, IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int interval = Math.Max(1, configuration.GetValue<int>( "Modbus:PollingIntervalSeconds", 1));

        logger.LogInformation("Modbus Worker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    List<Tenant> tenants;

                    using (var baseScope = scopeFactory.CreateScope())
                    {
                        var baseDbContext = baseScope.ServiceProvider.GetRequiredService<BaseDbContext>();

                        tenants = await baseDbContext.Tenants.AsNoTracking().Where(x => x.Status == TenantStatus.Active).ToListAsync(stoppingToken);
                    }

                    foreach (var tenant in tenants)
                    {
                        stoppingToken.ThrowIfCancellationRequested();

                        try
                        {
                            using var tenantScope =scopeFactory.CreateScope();

                            var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();

                            tenantContext.SetTenant(tenant.DatabaseName);

                            var configurationService = tenantScope.ServiceProvider.GetRequiredService<IDeviceConfigurationService>();

                            var telemetryService = tenantScope.ServiceProvider.GetRequiredService<IDeviceTelemetryService>();

                            var devices = await configurationService.GetActiveDevicesAsync(DeviceProtocol.ModbusTcp, stoppingToken);

                            foreach (var device in devices)
                            {
                                stoppingToken.ThrowIfCancellationRequested();

                                try
                                {
                                    logger.LogWarning(
                                        "MODBUS CONNECT ATTEMPT | Device: {Device} | Host: {Host} | Port: {Port}",
                                        device.DeviceCode,
                                        device.Host,
                                        device.Port);
                                    var telemetry = await reader.ReadAsync(device, stoppingToken);

                                    logger.LogInformation(
                                        "MODBUS TELEMETRY | Tenant: {Tenant} | " +
                                        "Device: {Device} | Production: {Production} | " +
                                        "Temperature: {Temperature} | " +
                                        "Current: {Current} | Scrap: {Scrap} | " +
                                        "Status: {Status}",
                                        tenant.Name,
                                        telemetry.DeviceId,
                                        telemetry.ProductionCount,
                                        telemetry.MotorTemperature,
                                        telemetry.MotorCurrent,
                                        telemetry.ScrapCount,
                                        telemetry.Status);

                                    await telemetryService.SaveAsync(telemetry, stoppingToken);

                                    logger.LogInformation(
                                        "Telemetry saved | Tenant: {Tenant} | Device: {Device}",
                                        tenant.Name,
                                        telemetry.DeviceId);
                                }
                                catch (OperationCanceledException)
                                    when (stoppingToken.IsCancellationRequested)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(
                                        ex,
                                        "Modbus device failed | Tenant: {Tenant} | Device: {Device}",
                                        tenant.Name,
                                        device.DeviceCode);
                                }
                            }
                        }
                        catch (OperationCanceledException)
                            when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Modbus tenant polling failed | Tenant: {Tenant}", tenant.Name);
                        }
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Modbus polling cycle failed.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {         
        }
        finally
        {
            logger.LogInformation("Modbus Worker stopped.");
        }
    }
}
