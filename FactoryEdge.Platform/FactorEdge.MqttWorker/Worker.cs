
using FactorEdge.MqttWorker.Services;

namespace FactorEdge.MqttWorker;

public sealed class Worker(ILogger<Worker> logger, MqttSubscriber subscriber) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MQTT Worker started.");

        try
        {
            await subscriber.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MQTT Worker stopped unexpectedly.");

            throw;
        }
    }
}
