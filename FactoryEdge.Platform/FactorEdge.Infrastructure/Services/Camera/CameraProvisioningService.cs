using FactorEdge.Application.DTOs.Cameras;
using FactorEdge.Application.Interfaces.Services.Camera;
using FactorEdge.Domain.Entities;
using FactorEdge.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using System.Runtime;
using System.Security.Cryptography;
using System.Text.Json;

namespace FactorEdge.Infrastructure.Services.Camera;
public sealed class CameraProvisioningService(
    IOptions<SimulatorProvisioningOptions> options,
    ILogger<CameraProvisioningService> logger)
    : ICameraProvisioningService
{
    private readonly SimulatorProvisioningOptions _settings = options.Value;

    private const string CreateScriptPath = "/home/onurtavukcu/factoredge-deploy/scripts/create-camera.sh";

    private const string DeleteScriptPath = "/home/onurtavukcu/factoredge-deploy/scripts/delete-camera.sh";

    public async Task<CameraProvisionResult> CreateAsync(Guid tenantId, Guid deviceId, string streamUrl, CancellationToken cancellationToken = default)
    {
        ValidateIds(tenantId, deviceId);
        ValidateSettings();

        if (string.IsNullOrWhiteSpace(streamUrl) ||
            streamUrl.Length > 2048 ||
            !Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            streamUrl.Any(char.IsWhiteSpace) ||
            streamUrl.Contains('@'))
        {
            throw new ArgumentException("Invalid camera stream URL.", nameof(streamUrl));
        }

        var commandText =
            $"{CreateScriptPath} " +
            $"{tenantId:D} " +
            $"{deviceId:D} " +
            $"{ShellQuote(streamUrl)}";

        logger.LogInformation(
            "Creating camera | Tenant: {TenantId} | Device: {DeviceId}",
            tenantId,
            deviceId);

        var output = await ExecuteCommandAsync(commandText, cancellationToken);

        var response = JsonSerializer.Deserialize<DockerCreateResponse>(
            output,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException(
                "Camera provisioning returned invalid JSON.");

        var expectedName = $"factoredge-camera-{deviceId:D}";
        var expectedTopic = $"factoredge/{tenantId:D}/camera/{deviceId:D}/status";

        if (!response.Success ||
            response.ContainerName != expectedName ||
            response.Topic != expectedTopic)
        {
            throw new InvalidOperationException(
                "Camera provisioning returned unexpected container details.");
        }

        logger.LogInformation(
            "Camera created | Container: {Container} | Topic: {Topic}",
            response.ContainerName,
            response.Topic);

        return new CameraProvisionResult
        {
            ContainerName = response.ContainerName,
            Topic = response.Topic
        };
    }

    public async Task DeleteAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default) 
    {
        ValidateIds(tenantId, deviceId);
        ValidateSettings();

        var commandText =
            $"{DeleteScriptPath} {tenantId:D} {deviceId:D}";

        var output = await ExecuteCommandAsync(
            commandText,
            cancellationToken);

        var response = JsonSerializer.Deserialize<DockerDeleteResponse>(
            output,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException(
                "Camera deletion returned invalid JSON.");

        if (!response.Success)
        {
            throw new InvalidOperationException(
                "Camera deletion was not successful.");
        }

        logger.LogInformation(
            "Camera deletion completed | Tenant: {TenantId} | Device: {DeviceId} | Removed: {Removed}",
            tenantId,
            deviceId,
            response.Removed);
    }

    private async Task<string> ExecuteCommandAsync(string commandText, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var key = new PrivateKeyFile(
                _settings.PrivateKeyPath);

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
                var fingerprint = _settings.HostKeySha256.Trim().Replace("SHA256:", "", StringComparison.Ordinal);

                fingerprint = fingerprint.PadRight((fingerprint.Length + 3) / 4 * 4, '=');

                var expectedHash = Convert.FromBase64String(fingerprint);

                var actualHash = SHA256.HashData(e.HostKey);

                e.CanTrust =
                    string.Equals(
                        e.HostKeyName,
                        "ssh-ed25519",
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

            var output = command.Execute();

            if (command.ExitStatus != 0)
            {
                throw new InvalidOperationException($"Camera provisioning command failed: {command.Error}");
            }

            return output.Trim();
        }, cancellationToken);
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.Username) ||
            _settings.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(_settings.HostKeySha256) ||
            string.IsNullOrWhiteSpace(_settings.PrivateKeyPath) ||
            !File.Exists(_settings.PrivateKeyPath))
        {
            throw new InvalidOperationException(
                "Camera SSH provisioning configuration is invalid.");
        }
    }

    private static void ValidateIds(Guid tenantId, Guid deviceId)
    {
        if (tenantId == Guid.Empty || deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId and DeviceId are required.");
        }
    }

    // POSIX shell parametresini güvenli tek argüman haline getirir.
    private static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", "'\\''") + "'";
    }

    private sealed class DockerCreateResponse
    {
        public bool Success { get; set; }
        public string ContainerName { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
    }

    private sealed class DockerDeleteResponse
    {
        public bool Success { get; set; }
        public bool Removed { get; set; }
    }
}