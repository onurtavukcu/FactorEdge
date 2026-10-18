
using FactorEdge.Contracts.Enums;
using FactorEdge.MqttSimulator.Services;

namespace FactorEdge.MqttSimulator;

public sealed class Worker(ILogger<Worker> logger, ProductionLineSimulator simulator, MqttPublisher publisher) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        int elapsedSeconds = 0;

        logger.LogInformation("MQTT Simulator started.");

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                int cycleSecond = elapsedSeconds % 40;

                ProductionLineStatus status = cycleSecond switch
                {
                    < 20 => ProductionLineStatus.Running,
                    < 30 => ProductionLineStatus.Stopped,
                    _ => ProductionLineStatus.Fault
                };

                simulator.SetStatus(status);

                var telemetry = simulator.Generate();

                try
                {
                    await publisher.PublishAsync(telemetry, stoppingToken);

                    logger.LogInformation(
                        "MQTT PUBLISHED | Device: {Device} | " +
                        "Production: {Production} | " +
                        "Temperature: {Temperature:F2} | " +
                        "Current: {Current:F2} | " +
                        "Scrap: {Scrap} | Status: {Status}",
                        telemetry.DeviceId,
                        telemetry.ProductionCount,
                        telemetry.MotorTemperature,
                        telemetry.MotorCurrent,
                        telemetry.ScrapCount,
                        telemetry.Status);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        "MQTT publish failed | Device: {Device} | Error: {Error}",
                        telemetry.DeviceId,
                        ex.Message);
                }

                elapsedSeconds++;
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            logger.LogInformation("MQTT Simulator Worker stopped.");
        }
    }
}
