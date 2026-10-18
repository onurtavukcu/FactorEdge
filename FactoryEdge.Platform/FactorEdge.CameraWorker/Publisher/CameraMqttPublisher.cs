using MQTTnet;
using MQTTnet.Protocol;
using System.Text.Json;

namespace FactorEdge.CameraWorker.Services;

public sealed class CameraMqttPublisher(
    IConfiguration configuration,
    ILogger<CameraMqttPublisher> logger) : IAsyncDisposable
{
    private readonly IMqttClient _client =
        new MqttClientFactory().CreateMqttClient();

    public async Task PublishAsync(string cameraId, string status, CancellationToken cancellationToken)
    {
        var host = configuration["Mqtt:Host"] ?? throw new InvalidOperationException("Mqtt:Host eksik.");

        var port = configuration.GetValue("Mqtt:Port", 1883);

        var topic = configuration["Mqtt:Topic"] ?? throw new InvalidOperationException("Mqtt:Topic eksik.");

        if (!_client.IsConnected)
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithClientId($"factoredge-camera-{cameraId}")
                .Build();

            await _client.ConnectAsync(options, cancellationToken);

            logger.LogInformation(
                "Camera MQTT bağlantısı kuruldu: {Host}:{Port}",
                host,
                port);
        }

        var payload = JsonSerializer.Serialize(new
        {
            cameraId,
            status,
            timestampUtc = DateTime.UtcNow
        });

        var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

        await _client.PublishAsync(message, cancellationToken);

        logger.LogInformation(
            "Camera MQTT yayınlandı | {CameraId} | {Status}",
            cameraId,
            status);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync();

        _client.Dispose();
    }
}