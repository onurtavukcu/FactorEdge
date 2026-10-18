using MQTTnet;
using System.Text.Json;
using MQTTnet.Protocol;
using FactorEdge.Contracts.Models;

namespace FactorEdge.MqttSimulator.Services
{
    public sealed class MqttPublisher : IDisposable
    {
        private readonly IMqttClient _client;
        private readonly MqttClientOptions _options;
        private readonly string _topic;

        public MqttPublisher(IConfiguration configuration)
        {
            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            var host = configuration["Mqtt:Host"]
                ?? throw new InvalidOperationException("MQTT Host missing");

            var port = configuration.GetValue<int>("Mqtt:Port", 1883);

            var tenantId = configuration.GetValue<Guid>("Simulator:TenantId");

            var deviceId = configuration["Simulator:DeviceId"];

            if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId))
            {
                throw new InvalidOperationException(
                    "Simulator TenantId or DeviceId missing.");
            }

            _topic = $"factory/{tenantId:D}/{deviceId}/telemetry";

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithClientId(configuration["Mqtt:ClientId"]
                    ?? "factoredge-line-2-simulator")
                .Build();
        }

        public async Task PublishAsync(ProductionLineTelemetry telemetry, CancellationToken cancellationToken)
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(_options, cancellationToken);
            }

            var json = JsonSerializer.Serialize(telemetry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_topic)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(message, cancellationToken);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
