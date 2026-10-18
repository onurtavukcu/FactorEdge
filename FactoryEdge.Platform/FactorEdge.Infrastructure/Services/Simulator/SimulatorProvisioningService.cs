
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FactorEdge.Application.DTOs.Simulator;
using FactorEdge.Application.Interfaces.Services.Simulator;
using FactorEdge.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace FactorEdge.Infrastructure.Services.Simulator;

public sealed class SimulatorProvisioningService(IOptions<SimulatorProvisioningOptions> options, ILogger<SimulatorProvisioningService> logger) : ISimulatorProvisioningService
{
    private readonly SimulatorProvisioningOptions _settings = options.Value;

    public async Task<SimulatorProvisionResult> CreateAsync(CreateSimulatorRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.DeviceId == Guid.Empty)
            throw new ArgumentException("TenantId and DeviceId are required.");

        if (!Regex.IsMatch(request.DeviceCode, @"^[a-zA-Z0-9_-]{1,64}$"))
            throw new ArgumentException("DeviceCode must contain only letters, numbers, '_' or '-'.");

        var protocol = request.Protocol.ToLowerInvariant() switch
        {
            "mqtt" => "mqtt",
            "modbus" or "modbustcp" => "modbus",
            _ => throw new ArgumentException("Unsupported simulator protocol.")
        };

        var unitId = protocol == "modbus"
            ? request.UnitId ?? throw new ArgumentException("UnitId is required.")
            : (byte)1;

        if (unitId is < 1 or > 247)
            throw new ArgumentException("Invalid Modbus UnitId.");

        ValidateSettings();

        // Komut parametrelerinin tamamı doğrulanmış değerlerden üretilir.
        var commandText =
            $"{_settings.ScriptPath} " +
            $"{protocol} " +
            $"{request.TenantId:D} " +
            $"{request.DeviceId:D} " +
            $"{request.DeviceCode} " +
            $"{unitId}";

        logger.LogInformation(
            "Creating simulator | Tenant: {TenantId} | Device: {DeviceId}",
            request.TenantId,
            request.DeviceId);

        var output = await Task.Run(() =>
        {
            using var key = new PrivateKeyFile(_settings.PrivateKeyPath);

            var connectionInfo = new ConnectionInfo(
                _settings.Host,
                _settings.Port,
                _settings.Username,
                new PrivateKeyAuthenticationMethod(_settings.Username, key))
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            using var client = new SshClient(connectionInfo);

            client.HostKeyReceived += (_, e) =>
            {
                const string expectedAlgorithm = "ssh-ed25519";

                var fingerprint = _settings.HostKeySha256.Trim();
                fingerprint = fingerprint.Replace("SHA256:", "");

                fingerprint = fingerprint.PadRight((fingerprint.Length + 3) / 4 * 4, '=');

                var expectedHash = Convert.FromBase64String(fingerprint);
                var actualHash = SHA256.HashData(e.HostKey);

                e.CanTrust =
                    string.Equals(
                        e.HostKeyName,
                        expectedAlgorithm,
                        StringComparison.Ordinal) &&
                    actualHash.Length == expectedHash.Length &&
                    CryptographicOperations.FixedTimeEquals(
                        actualHash,
                        expectedHash);
            };

            using var registration = cancellationToken.Register(client.Dispose);

            client.Connect();

            using var command = client.CreateCommand(commandText);
            command.CommandTimeout = TimeSpan.FromSeconds(30);

            var result = command.Execute();

            if (command.ExitStatus != 0)
            {
                throw new InvalidOperationException(
                    $"Simulator creation failed: {command.Error}");
            }

            return result;
        }, cancellationToken);

        var provisioned = JsonSerializer.Deserialize<DockerCreateResponse>(
            output,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException(
                "Docker provisioning returned an invalid response.");

        if (string.IsNullOrWhiteSpace(provisioned.ContainerName) || provisioned.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Docker provisioning returned incomplete connection details.");
        }

        var result = new SimulatorProvisionResult
        {
            ContainerName = provisioned.ContainerName,
            Host = _settings.Host,
            Port = provisioned.Port,
            UnitId = protocol == "modbus" ? unitId : null,
            Topic = protocol == "mqtt"
                ? $"factory/{request.TenantId:D}/{request.DeviceCode}/telemetry"
                : null
        };

        logger.LogInformation(
            "Simulator created | Container: {Container} | Host: {Host} | Port: {Port}",
            result.ContainerName,
            result.Host,
            result.Port);

        return result;
    }


    public async Task SoftDeleteAsync(
        Guid tenantId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || deviceId == Guid.Empty)
            throw new ArgumentException("TenantId and DeviceId are required.");

        ValidateSettings();

        const string scriptPath = "/home/onurtavukcu/factoredge-deploy/scripts/delete-simulator.sh";

        var commandText = $"{scriptPath} {tenantId:D} {deviceId:D}";

        await Task.Run(() =>
        {
            using var key = new PrivateKeyFile(_settings.PrivateKeyPath);

            var connectionInfo = new ConnectionInfo(
                _settings.Host,
                _settings.Port,
                _settings.Username,
                new PrivateKeyAuthenticationMethod(
                    _settings.Username,
                    key))
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            using var client = new SshClient(connectionInfo);

            client.HostKeyReceived += (_, e) =>
            {
                var fingerprint = _settings.HostKeySha256.Trim()
                    .Replace("SHA256:", "");

                fingerprint = fingerprint.PadRight(
                    (fingerprint.Length + 3) / 4 * 4, '=');

                var expectedHash =
                    Convert.FromBase64String(fingerprint);

                var actualHash = SHA256.HashData(e.HostKey);

                e.CanTrust =
                    e.HostKeyName == "ssh-ed25519" &&
                    actualHash.Length == expectedHash.Length &&
                    CryptographicOperations.FixedTimeEquals(
                        actualHash,
                        expectedHash);
            };

            using var registration =
                cancellationToken.Register(client.Dispose);

            client.Connect();

            using var command = client.CreateCommand(commandText);
            command.CommandTimeout = TimeSpan.FromSeconds(30);

            var output = command.Execute();

            if (command.ExitStatus != 0)
            {
                throw new InvalidOperationException($"Simulator deletion failed: {command.Error}");
            }

            logger.LogInformation(
                "Simulator deleted | Tenant: {TenantId} | " +
                "Device: {DeviceId} | Response: {Response}",
                tenantId,
                deviceId,
                output);
        }, cancellationToken);
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host))
            throw new InvalidOperationException("Simulator SSH Host is missing.");

        if (string.IsNullOrWhiteSpace(_settings.Username))
            throw new InvalidOperationException("Simulator SSH Username is missing.");

        if (_settings.Port is < 1 or > 65535)
            throw new InvalidOperationException("Invalid SSH port.");

        if (string.IsNullOrWhiteSpace(_settings.PrivateKeyPath) || !File.Exists(_settings.PrivateKeyPath))
            throw new InvalidOperationException("Simulator SSH private key not found.");

        if (string.IsNullOrWhiteSpace(_settings.ScriptPath))
            throw new InvalidOperationException("Simulator provisioning script path is missing.");

        if (string.IsNullOrWhiteSpace(_settings.HostKeySha256))
            throw new InvalidOperationException("SSH host key fingerprint is missing.");
    }
    private sealed class DockerCreateResponse
    {
        public string ContainerName { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public int Port { get; set; }
    }
}
