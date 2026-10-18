using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Domain.Entities;

namespace FactorEdge.Web.Background_Services;

public sealed class DeviceConnectionMonitor(ILogger<DeviceConnectionMonitor> logger, IServiceScopeFactory scopeFactory, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int checkInterval = Math.Max(1, configuration.GetValue<int>("DeviceMonitoring:CheckIntervalSeconds", 2));

        int timeout = Math.Max(1, configuration.GetValue<int>( "DeviceMonitoring:TimeoutSeconds", 5));

        logger.LogInformation("Device Connection Monitor started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(checkInterval));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    List<Tenant> tenants;

                    using (var baseScope = scopeFactory.CreateScope())
                    {
                        var platformRepository = baseScope.ServiceProvider.GetRequiredService<IPlatformRepository>();

                        tenants = await platformRepository.GetActiveTenantsAsync(
                            stoppingToken);
                    }

                    foreach (var tenant in tenants)
                    {
                        stoppingToken.ThrowIfCancellationRequested();

                        try
                        {
                            using var tenantScope =scopeFactory.CreateScope();

                            var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();

                            tenantContext.SetTenant(tenant.DatabaseName);

                            var connectionService = tenantScope.ServiceProvider.GetRequiredService<IDeviceConnectionStatusService>();

                            int affectedRows =await connectionService .MarkStaleDevicesAsync(TimeSpan.FromSeconds(timeout),stoppingToken);

                            if (affectedRows > 0)
                            {
                                logger.LogWarning(
                                    "{Count} device(s) marked as Disconnected | " +
                                    "Tenant: {Tenant}",
                                    affectedRows,
                                    tenant.Name);
                            }
                        }
                        catch (OperationCanceledException)
                            when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Device monitoring failed | Tenant: {Tenant}", tenant.Name);
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
                    logger.LogError(ex, "Device connection monitoring cycle failed.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Ctrl+C ile normal kapanış.
        }
        finally
        {
            logger.LogInformation("Device Connection Monitor stopped.");
        }
    }
}
