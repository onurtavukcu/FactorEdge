
namespace FactorEdge.MqttWorker.Services;

public sealed class CameraSubscriberWorker(
    ILogger<CameraSubscriberWorker> logger,
    CameraMqttSubscriber subscriber) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Camera MQTT Worker started.");

        try
        {
            await subscriber.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Camera MQTT Worker stopped unexpectedly.");

            throw;
        }
    }
}
