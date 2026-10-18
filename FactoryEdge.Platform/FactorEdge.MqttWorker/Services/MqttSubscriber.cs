
using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Application.Interfaces.Services;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Contracts.Models;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace FactorEdge.MqttWorker.Services;

public sealed class MqttSubscriber(ILogger<MqttSubscriber> logger, IServiceScopeFactory scopeFactory)
{
    private readonly Dictionary<string, BrokerSession> _sessions = new();

    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private sealed record TopicTarget(
        Guid TenantId,
        string DatabaseName,
        string DeviceCode);

    private sealed record TenantMqttDevice(
        DeviceConnectionDto Device,
        TopicTarget Target);

    private sealed class BrokerSession
    {
        public required IMqttClient Client { get; init; }

        public ConcurrentDictionary<string, TopicTarget> Topics { get; }
            = new(StringComparer.Ordinal);

        public int IsClosing;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    List<TenantMqttDevice> devices = new();

                    bool configurationComplete = true;

                    // Merkezi DB'den aktif tenant listesini al.
                    using (var scope = scopeFactory.CreateScope())
                    {
                        var baseDb = scope.ServiceProvider
                            .GetRequiredService<BaseDbContext>();

                        var tenants = await baseDb.Tenants
                            .AsNoTracking()
                            .Where(x => x.Status == TenantStatus.Active)
                            .ToListAsync(cancellationToken);

                        foreach (var tenant in tenants)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                using var tenantScope =
                                    scopeFactory.CreateScope();

                                var tenantContext = tenantScope.ServiceProvider
                                    .GetRequiredService<ITenantContext>();

                                tenantContext.SetTenant(
                                    tenant.DatabaseName);

                                var configurationService =
                                    tenantScope.ServiceProvider
                                        .GetRequiredService<
                                            IDeviceConfigurationService>();

                                var tenantDevices =
                                    await configurationService
                                        .GetActiveDevicesAsync(
                                            DeviceProtocol.Mqtt,
                                            cancellationToken);

                                devices.AddRange(
                                    tenantDevices.Select(device =>
                                        new TenantMqttDevice(
                                            device,
                                            new TopicTarget(
                                                tenant.Id,
                                                tenant.DatabaseName,
                                                device.DeviceCode))));
                            }
                            catch (OperationCanceledException)
                                when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                configurationComplete = false;

                                logger.LogError(
                                    ex,
                                    "MQTT tenant configuration failed | Tenant: {Tenant}",
                                    tenant.Name);
                            }
                        }
                    }

                    // Eksik tenant verisi yüzünden mevcut
                    // abonelikleri yanlışlıkla kaldırma.
                    if (configurationComplete)
                    {
                        await SynchronizeAsync(
                            devices,
                            cancellationToken);
                    }
                    else
                    {
                        logger.LogWarning(
                            "MQTT sync skipped because some tenant configurations could not be loaded.");
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "MQTT configuration synchronization failed.");
                }

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(3),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            foreach (var session in _sessions.Values.ToList())
            {
                await CloseSessionAsync(session);
            }

            _sessions.Clear();

            logger.LogInformation("MQTT Subscriber stopped.");
        }
    }

    private async Task SynchronizeAsync(
        List<TenantMqttDevice> devices,
        CancellationToken cancellationToken)
    {
        var groups = devices
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Device.Host) &&
                x.Device.Port is > 0 and <= 65535 &&
                !string.IsNullOrWhiteSpace(x.Device.Topic))
            .GroupBy(x =>
                $"{x.Device.Host}:{x.Device.Port}")
            .ToDictionary(
                x => x.Key,
                x => x.ToList());

        // Aktif cihazı kalmayan broker bağlantılarını kapat.
        foreach (var key in _sessions.Keys.ToList())
        {
            if (!groups.ContainsKey(key))
            {
                var session = _sessions[key];

                _sessions.Remove(key);

                await CloseSessionAsync(session);

                logger.LogInformation(
                    "MQTT BROKER REMOVED | Broker: {Broker}",
                    key);
            }
        }

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var devicesInBroker = group.Value;

            // Aynı broker üzerinde aynı topic iki farklı
            // cihaza veya tenant'a atanamaz.
            var duplicateTopics = devicesInBroker
                .GroupBy(
                    x => x.Device.Topic!,
                    StringComparer.Ordinal)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var topic in duplicateTopics)
            {
                logger.LogWarning(
                    "Duplicate MQTT topic | Broker: {Broker} | Topic: {Topic}",
                    group.Key,
                    topic);
            }

            var desiredTopics = devicesInBroker
                .Where(x =>
                    !duplicateTopics.Contains(x.Device.Topic!))
                .ToDictionary(
                    x => x.Device.Topic!,
                    x => x.Target,
                    StringComparer.Ordinal);

            if (_sessions.TryGetValue(
                group.Key,
                out var existing))
            {
                if (existing.Client.IsConnected)
                {
                    try
                    {
                        await SynchronizeTopicsAsync(
                            group.Key,
                            existing,
                            desiredTopics,
                            cancellationToken);
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
                            "MQTT subscription synchronization failed: {Broker}",
                            group.Key);

                        _sessions.Remove(group.Key);

                        await CloseSessionAsync(existing);
                    }

                    continue;
                }

                _sessions.Remove(group.Key);

                await CloseSessionAsync(existing);
            }

            if (desiredTopics.Count == 0)
                continue;

            var firstDevice = devicesInBroker[0].Device;

            await ConnectBrokerAsync(
                group.Key,
                firstDevice.Host,
                firstDevice.Port,
                desiredTopics,
                cancellationToken);
        }
    }

    private async Task ConnectBrokerAsync(
        string brokerKey,
        string host,
        int port,
        Dictionary<string, TopicTarget> topics,
        CancellationToken cancellationToken)
    {
        var factory = new MqttClientFactory();
        var client = factory.CreateMqttClient();

        var session = new BrokerSession
        {
            Client = client
        };

        client.ApplicationMessageReceivedAsync += args =>
            OnMessageReceivedAsync(
                args,
                session,
                cancellationToken);

        try
        {
            var clientId = $"factoredge-{Guid.NewGuid():N}";

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithClientId(clientId)
                .WithProtocolVersion(
                    MQTTnet.Formatter.MqttProtocolVersion.V500)
                .Build();

            await client.ConnectAsync(
                options,
                cancellationToken);

            logger.LogInformation(
                "MQTT CONNECTED | Broker: {Broker}",
                brokerKey);

            var subscriptionBuilder =
                new MqttClientSubscribeOptionsBuilder();

            foreach (var topic in topics.Keys)
            {
                subscriptionBuilder.WithTopicFilter(topic);
            }

            var subscribeResult = await client.SubscribeAsync(
                subscriptionBuilder.Build(),
                cancellationToken);

            if (subscribeResult.Items.Count != topics.Count)
            {
                throw new InvalidOperationException(
                    $"MQTT SUBACK item count mismatch: {brokerKey}");
            }

            foreach (var item in subscribeResult.Items)
            {
                if ((int)item.ResultCode >= 128)
                {
                    throw new InvalidOperationException(
                        $"MQTT SUBACK rejected | Broker: {brokerKey} " +
                        $"Topic: {item.TopicFilter.Topic} " +
                        $"Result: {item.ResultCode}");
                }

                logger.LogInformation(
                    "MQTT SUBACK ACCEPTED | Broker: {Broker} | " +
                    "Topic: {Topic} | Result: {Result}",
                    brokerKey,
                    item.TopicFilter.Topic,
                    item.ResultCode);
            }

            foreach (var topic in topics)
            {
                session.Topics[topic.Key] = topic.Value;
            }

            _sessions[brokerKey] = session;

            logger.LogInformation(
                "MQTT SUBSCRIBED | Broker: {Broker} | Topics: {Count}",
                brokerKey,
                topics.Count);
        }
        catch (Exception ex)
        {
            _sessions.Remove(brokerKey);

            await CloseSessionAsync(session);

            if (ex is OperationCanceledException &&
                cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(
                ex,
                "MQTT broker connection or subscription failed: {Broker}",
                brokerKey);
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args, BrokerSession session, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref session.IsClosing) == 1)
            {
                return;
            }

            string topic = args.ApplicationMessage.Topic;

            if (!session.Topics.TryGetValue(topic, out var target)) 
            {
                logger.LogWarning("Unknown MQTT topic: {Topic}", topic);

                return;
            }

            var json = Encoding.UTF8.GetString(args.ApplicationMessage.Payload.ToArray());

            var telemetry = JsonSerializer.Deserialize<ProductionLineTelemetry>(json, _jsonOptions);

            if (telemetry is null)
            {
                logger.LogWarning("Empty MQTT telemetry: {Topic}", topic);

                return;
            }

            if (!string.Equals(telemetry.DeviceId, target.DeviceCode,StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "MQTT device mismatch | Topic: {Topic} | " +
                    "Expected: {Expected} | Received: {Received}",
                    topic,
                    target.DeviceCode,
                    telemetry.DeviceId);

                return;
            }

            if (telemetry.TenantId != target.TenantId)
            {
                logger.LogWarning(
                    "MQTT tenant mismatch | Topic: {Topic} | " +
                    "Expected: {Expected} | Received: {Received}",
                    topic,
                    target.TenantId,
                    telemetry.TenantId);

                return;
            }

            logger.LogInformation(
                "MQTT TELEMETRY | Tenant: {TenantId} | " +
                "Device: {Device} | Production: {Production} | " +
                "Temperature: {Temperature} | " +
                "Current: {Current} | Status: {Status}",
                target.TenantId,
                telemetry.DeviceId,
                telemetry.ProductionCount,
                telemetry.MotorTemperature,
                telemetry.MotorCurrent,
                telemetry.Status);

            using var scope = scopeFactory.CreateScope();

            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

            tenantContext.SetTenant(target.DatabaseName);

            var telemetryService = scope.ServiceProvider.GetRequiredService<IDeviceTelemetryService>();

            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref session.IsClosing) == 1)
            {
                return;
            }

            await telemetryService.SaveAsync(telemetry, cancellationToken);

            logger.LogInformation("MQTT telemetry saved | Tenant: {TenantId} | Device: {Device}", target.TenantId, telemetry.DeviceId);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid MQTT telemetry JSON.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MQTT telemetry processing failed.");
        }
    }

    private async Task SynchronizeTopicsAsync(
        string brokerKey,
        BrokerSession session,
        Dictionary<string, TopicTarget> desiredTopics,
        CancellationToken cancellationToken)
    {
        var removedTopics = session.Topics.Keys
            .Except(
                desiredTopics.Keys,
                StringComparer.Ordinal)
            .ToList();

        var addedTopics = desiredTopics.Keys
            .Except(
                session.Topics.Keys,
                StringComparer.Ordinal)
            .ToList();

        var changedMappings = desiredTopics.Keys
            .Intersect(
                session.Topics.Keys,
                StringComparer.Ordinal)
            .Where(topic =>
                session.Topics.TryGetValue(
                    topic,
                    out var currentTarget) &&
                currentTarget != desiredTopics[topic])
            .ToList();

        // 1. UNSUBSCRIBE
        foreach (var topic in removedTopics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new MqttClientUnsubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            var result = await session.Client.UnsubscribeAsync(
                options,
                cancellationToken);

            if (result.Items.Count != 1 ||
                result.Items.Any(x => (int)x.ResultCode >= 128))
            {
                throw new InvalidOperationException(
                    $"MQTT UNSUBACK rejected | Broker: {brokerKey} " +
                    $"Topic: {topic}");
            }

            session.Topics.TryRemove(topic, out _);

            logger.LogInformation(
                "MQTT UNSUBSCRIBED | Broker: {Broker} | Topic: {Topic}",
                brokerKey,
                topic);
        }

        // 2. SUBSCRIBE
        foreach (var topic in addedTopics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topic).Build();

            var result = await session.Client.SubscribeAsync(options, cancellationToken);

            if (result.Items.Count != 1 || result.Items.Any(x => (int)x.ResultCode >= 128))
            {
                throw new InvalidOperationException($"MQTT SUBACK rejected | Broker: {brokerKey} " + $"Topic: {topic}");
            }

            session.Topics[topic] = desiredTopics[topic];

            logger.LogInformation("MQTT SUBSCRIBED | Broker: {Broker} | Topic: {Topic}", brokerKey,
                topic);
        }

        // 3. Topic aynı, tenant veya cihaz mapping'i farklı.
        foreach (var topic in changedMappings)
        {
            session.Topics[topic] = desiredTopics[topic];

            logger.LogInformation(
                "MQTT MAPPING UPDATED | Topic: {Topic} | " +
                "Tenant: {TenantId} | Device: {Device}",
                topic,
                desiredTopics[topic].TenantId,
                desiredTopics[topic].DeviceCode);
        }
    }

    private async Task CloseSessionAsync(BrokerSession session)
    {
        if (Interlocked.Exchange(ref session.IsClosing,
            1) == 1)
        {
            return;
        }

        try
        {
            if (session.Client.IsConnected)
            {
                await session.Client.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "MQTT disconnect failed.");
        }
        finally
        {
            session.Topics.Clear();
            session.Client.Dispose();
        }
    }
}
