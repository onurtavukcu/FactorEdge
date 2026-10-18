
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace FactorEdge.MqttWorker.Services;

public sealed class CameraMqttSubscriber(ILogger<CameraMqttSubscriber> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
{
    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private sealed class CameraStatusMessage
    {
        public string CameraId { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? TimestampUtc { get; set; }
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var host = configuration["CameraMqtt:Host"]
            ?? throw new InvalidOperationException(
                "CameraMqtt:Host tanımlanmamış.");

        var port = configuration.GetValue(
            "CameraMqtt:Port", 1883);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new MqttClientFactory();

                using var client = factory.CreateMqttClient();

                client.ApplicationMessageReceivedAsync += async args =>
                {
                    await ProcessMessageAsync(
                        args.ApplicationMessage.Topic,
                        args.ApplicationMessage.Payload.ToArray(),
                        stoppingToken);
                };

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(host, port)
                    .WithClientId($"factoredge-camera-consumer-{Guid.NewGuid():N}")
                    .WithProtocolVersion(
                        MQTTnet.Formatter.MqttProtocolVersion.V500)
                    .Build();

                await client.ConnectAsync(
                    options,
                    stoppingToken);

                logger.LogInformation(
                    "Camera MQTT connected: {Host}:{Port}",
                    host,
                    port);

                var subscribeOptions =
                    new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(
                            "factoredge/+/camera/+/status")
                        .Build();

                var subscribeResult = await client.SubscribeAsync(
                    subscribeOptions,
                    stoppingToken);

                if (subscribeResult.Items.Count != 1 ||
                    subscribeResult.Items.Any(
                        x => (int)x.ResultCode >= 128))
                {
                    throw new InvalidOperationException(
                        "Camera MQTT subscription rejected.");
                }

                logger.LogInformation(
                    "Camera MQTT subscribed: factoredge/+/camera/+/status");

                while (!stoppingToken.IsCancellationRequested &&
                       client.IsConnected)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(2),
                        stoppingToken);
                }

                if (client.IsConnected)
                    await client.DisconnectAsync();
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Camera MQTT connection failed. Retrying...");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(3),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation(
            "Camera MQTT subscriber stopped.");
    }

    private async Task ProcessMessageAsync(
        string topic,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        try
        {
            // factoredge/{tenantId}/camera/{deviceId}/status
            var parts = topic.Split('/');

            if (parts.Length != 5 ||
                parts[0] != "factoredge" ||
                parts[2] != "camera" ||
                parts[4] != "status" ||
                !Guid.TryParse(parts[1], out var tenantId) ||
                !Guid.TryParse(parts[3], out var deviceId))
            {
                logger.LogWarning(
                    "Invalid camera topic: {Topic}",
                    topic);

                return;
            }

            if (payload.Length == 0 || payload.Length > 4096)
            {
                logger.LogWarning(
                    "Invalid camera payload length: {Topic}",
                    topic);

                return;
            }

            var json = Encoding.UTF8.GetString(payload);

            var message = JsonSerializer.Deserialize<CameraStatusMessage>(
                json,
                _jsonOptions);

            if (message is null ||
                !Guid.TryParse(message.CameraId, out var payloadDeviceId) ||
                payloadDeviceId != deviceId)
            {
                logger.LogWarning(
                    "Camera ID mismatch: {Topic}",
                    topic);

                return;
            }

            if (message.Status is not
                ("Normal" or "Fatigued" or "Absent"))
            {
                logger.LogWarning(
                    "Invalid camera status: {Status}",
                    message.Status);

                return;
            }

            // TenantId'yi yalnızca payload'dan kabul etmiyoruz.
            // Merkezi DB üzerinden gerçekten aktif olduğunu doğruluyoruz.
            string databaseName;

            using (var scope = scopeFactory.CreateScope())
            {
                var baseDb = scope.ServiceProvider
                    .GetRequiredService<BaseDbContext>();

                var tenant = await baseDb.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == tenantId &&
                             x.Status == TenantStatus.Active,
                        cancellationToken);

                if (tenant is null)
                {
                    logger.LogWarning(
                        "Camera message rejected. Tenant not found: {TenantId}",
                        tenantId);

                    return;
                }

                databaseName = tenant.DatabaseName;
            }

            // TenantDbContext doğru tenant veritabanına bağlanacak.
            using var tenantScope = scopeFactory.CreateScope();

            var tenantContext = tenantScope.ServiceProvider
                .GetRequiredService<ITenantContext>();

            tenantContext.SetTenant(databaseName);

            var tenantDb = tenantScope.ServiceProvider
                .GetRequiredService<TenantDbContext>();

            var camera = await tenantDb.DeviceCameras
                .Include(x => x.Device)
                .FirstOrDefaultAsync(
                    x => x.DeviceId == deviceId &&
                         x.Device.DeviceType == DeviceType.Camera &&
                         x.Device.IsActive &&
                         !x.Device.IsDeleted,
                    cancellationToken);

            if (camera is null)
            {
                logger.LogWarning(
                    "Camera not found or inactive | Tenant: {TenantId} | Device: {DeviceId}",
                    tenantId,
                    deviceId);

                return;
            }

            // Veritabanındaki topic ile gelen topic de eşleşmeli.
            if (!string.Equals(
                camera.Device.Topic,
                topic,
                StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Camera topic mismatch | Device: {DeviceId}",
                    deviceId);

                return;
            }



            var now = DateTime.UtcNow;

            var previousStatus = camera.LastOperatorStatus;

            var statusChanged = !string.Equals(
                previousStatus,
                message.Status,
                StringComparison.Ordinal);

            // Her başarılı MQTT mesajı kameranın canlı olduğunu gösterir.
            camera.Device.ConnectionStatus =
                DeviceConnectionStatus.Connected;

            camera.Device.LastSeenUtc = now;

            camera.LastOperatorStatus = message.Status;
            camera.LastStatusAtUtc = now;

            // History tablosuna sadece durum geçişlerinde kayıt at.
            if (statusChanged)
            {
                tenantDb.CameraStatusHistories.Add(
                    new CameraStatusHistory
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = deviceId,
                        Status = message.Status,
                        CreatedAtUtc = now
                    });
            }

            await tenantDb.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "CAMERA STATUS SAVED | Tenant: {TenantId} | Camera: {DeviceId} | Status: {Status} | Changed: {Changed}",
                tenantId,
                deviceId,
                message.Status,
                statusChanged);

        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Invalid camera MQTT JSON.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Camera MQTT processing failed.");
        }
    }
}
