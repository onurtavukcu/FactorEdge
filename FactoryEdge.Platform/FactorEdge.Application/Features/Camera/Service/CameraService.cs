
using FactorEdge.Application.DTOs.Cameras;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Camera;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FactorEdge.Application.Features.Camera.Service;

public sealed class CameraService(
    IDeviceRepository deviceRepository,
    ICameraRepository cameraRepository,
    ICameraProvisioningService provisioningService,
    ILogger<CameraService> logger) : ICameraService
{
    public async Task<CameraResponse> CreateAsync(Guid tenantId, CreateCameraRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        var code = request.DeviceCode?.Trim() ?? "";
        var name = request.Name?.Trim() ?? "";
        var streamUrl = request.StreamUrl?.Trim() ?? "";

        ValidateCode(code);
        ValidateName(name);
        var uri = ParseStreamUrl(streamUrl);

        var existing = await deviceRepository.GetByCodeAsync(code, cancellationToken);

        if (existing is not null)
            throw new InvalidOperationException(
                "Bu kamera/cihaz kodu zaten kullanılıyor.");

        var now = DateTime.UtcNow;
        var deviceId = Guid.NewGuid();

        var device = new Domain.Entities.Device
        {
            Id = deviceId,
            DeviceCode = code,
            Name = name,
            DeviceType = DeviceType.Camera,
            Protocol = DeviceProtocol.Mqtt,
            Host = uri.Host,
            Port = uri.Port,
            Topic = BuildTopic(tenantId, deviceId),
            ConnectionStatus = DeviceConnectionStatus.Disconnected,
            IsActive = true,
            IsDeleted = false,
            CreatedAtUtc = now
        };

        var camera = new DeviceCamera
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            StreamUrl = streamUrl,
            LastOperatorStatus = "Unknown",
            CreatedAtUtc = now
        };

        // Önce Docker, ardından veritabanı.
        // Veritabanı başarısız olursa Docker işlemi geri alınır.
        var provisioned = await provisioningService.CreateAsync(
            tenantId,
            deviceId,
            streamUrl,
            cancellationToken);

        try
        {
            camera.ContainerName = provisioned.ContainerName;
            device.Topic = provisioned.Topic;

            await using var transaction = await deviceRepository.BeginTransactionAsync(cancellationToken);

            await deviceRepository.AddAsync(device, cancellationToken);

            await cameraRepository.AddAsync(camera, cancellationToken);

            await cameraRepository.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Map(camera, device);
        }
        catch
        {
            try
            {
                await provisioningService.DeleteAsync(
                    tenantId,
                    deviceId,
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                logger.LogError(
                    cleanupException,
                    "Kamera DB kaydı başarısız ve Docker temizliği yapılamadı. Device: {DeviceId}",
                    deviceId);
            }

            throw;
        }
    }

    public async Task<List<CameraResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var cameras = await cameraRepository.GetAllAsync(
            cancellationToken);

        return cameras
            .Select(x => Map(x, x.Device))
            .ToList();
    }

    public async Task<CameraResponse?> GetByIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var camera = await cameraRepository.GetByDeviceIdAsync(
            deviceId, cancellationToken);

        return camera is null
            ? null
            : Map(camera, camera.Device);
    }

    public async Task<CameraResponse?> UpdateAsync(
        Guid tenantId,
        Guid deviceId,
        UpdateCameraRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        var camera = await cameraRepository.GetTrackedByDeviceIdAsync(
            deviceId, cancellationToken);

        if (camera is null)
            return null;

        var name = request.Name?.Trim() ?? "";
        var streamUrl = request.StreamUrl?.Trim() ?? "";

        ValidateName(name);
        var uri = ParseStreamUrl(streamUrl);

        var device = camera.Device;

        var oldName = device.Name;
        var oldHost = device.Host;
        var oldPort = device.Port;
        var oldIsActive = device.IsActive;
        var oldStreamUrl = camera.StreamUrl;
        var oldContainerName = camera.ContainerName;
        var oldConnectionStatus = device.ConnectionStatus;
        var oldOperatorStatus = camera.LastOperatorStatus;
        var oldStatusAtUtc = camera.LastStatusAtUtc;

        var hadContainer =
            !string.IsNullOrWhiteSpace(oldContainerName);

        var needsRestart =
            oldIsActive != request.IsActive ||
            !string.Equals(
                oldStreamUrl,
                streamUrl,
                StringComparison.Ordinal) ||
            (request.IsActive && !hadContainer);

        var dockerChanged = false;

        try
        {
            if (needsRestart)
            {
                if (hadContainer)
                {
                    await provisioningService.DeleteAsync(
                        tenantId,
                        deviceId,
                        cancellationToken);

                    dockerChanged = true;
                }

                if (request.IsActive)
                {
                    var provisioned =
                        await provisioningService.CreateAsync(
                            tenantId,
                            deviceId,
                            streamUrl,
                            cancellationToken);

                    dockerChanged = true;
                    camera.ContainerName = provisioned.ContainerName;
                    device.Topic = provisioned.Topic;
                }
                else
                {
                    camera.ContainerName = null;
                }

                device.ConnectionStatus =
                    DeviceConnectionStatus.Disconnected;

                camera.LastOperatorStatus = "Unknown";
                camera.LastStatusAtUtc = null;
            }

            device.Name = name;
            device.Host = uri.Host;
            device.Port = uri.Port;
            device.IsActive = request.IsActive;
            camera.StreamUrl = streamUrl;

            await cameraRepository.SaveChangesAsync(
                cancellationToken);

            return Map(camera, device);
        }
        catch
        {
            // Docker değişmiş ama işlem tamamlanamamışsa
            // mümkün olduğunca önceki durumuna dön.
            if (dockerChanged)
            {
                try
                {
                    await provisioningService.DeleteAsync(
                        tenantId,
                        deviceId,
                        CancellationToken.None);

                    if (hadContainer)
                    {
                        var restored =
                            await provisioningService.CreateAsync(
                                tenantId,
                                deviceId,
                                oldStreamUrl,
                                CancellationToken.None);

                        camera.ContainerName =
                            restored.ContainerName;
                    }
                }
                catch (Exception recoveryException)
                {
                    logger.LogError(
                        recoveryException,
                        "Kamera güncellemesi geri alınamadı. Device: {DeviceId}",
                        deviceId);
                }
            }

            // Tracked entity değerlerini de geri al.
            device.Name = oldName;
            device.Host = oldHost;
            device.Port = oldPort;
            device.IsActive = oldIsActive;
            device.ConnectionStatus = oldConnectionStatus;

            camera.StreamUrl = oldStreamUrl;
            camera.ContainerName = oldContainerName;
            camera.LastOperatorStatus = oldOperatorStatus;
            camera.LastStatusAtUtc = oldStatusAtUtc;

            throw;
        }
    }

    public async Task<bool> DeleteAsync(
        Guid tenantId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantId(tenantId);

        var camera = await cameraRepository.GetTrackedByDeviceIdAsync(
            deviceId, cancellationToken);

        if (camera is null)
            return false;

        var oldStreamUrl = camera.StreamUrl;
        var hadContainer =
            !string.IsNullOrWhiteSpace(camera.ContainerName);

        // Önce Docker silinir.
        // SSH veya Docker hata verirse DB kaydına dokunulmaz.
        await provisioningService.DeleteAsync(
            tenantId,
            deviceId,
            cancellationToken);

        try
        {
            camera.Device.IsDeleted = true;
            camera.Device.IsActive = false;
            camera.Device.DeletedAtUtc = DateTime.UtcNow;
            camera.Device.ConnectionStatus =
                DeviceConnectionStatus.Disconnected;

            camera.ContainerName = null;

            await cameraRepository.SaveChangesAsync(
                cancellationToken);

            return true;
        }
        catch
        {
            if (hadContainer)
            {
                try
                {
                    await provisioningService.CreateAsync(
                        tenantId,
                        deviceId,
                        oldStreamUrl,
                        CancellationToken.None);
                }
                catch (Exception recoveryException)
                {
                    logger.LogError(
                        recoveryException,
                        "Kamera silme işlemi geri alınamadı. Device: {DeviceId}",
                        deviceId);
                }
            }

            throw;
        }
    }

    private static void ValidateTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException(
                "TenantId boş olamaz.");
    }

    private static void ValidateCode(string code)
    {
        if (!Regex.IsMatch(
            code,
            @"^[a-zA-Z0-9_-]{1,64}$"))
        {
            throw new ArgumentException(
                "Geçersiz kamera kodu.");
        }
    }

    private static void ValidateName(string name)
    {
        if (name.Length is < 1 or > 200)
            throw new ArgumentException(
                "Geçersiz kamera adı.");
    }

    private static Uri ParseStreamUrl(string streamUrl)
    {
        if (string.IsNullOrWhiteSpace(streamUrl) ||
            streamUrl.Length > 2048 ||
            !Uri.TryCreate(
                streamUrl,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException(
                "Geçerli bir HTTP/HTTPS kamera yayın adresi girilmelidir.");
        }

        return uri;
    }

    private static string BuildTopic(
        Guid tenantId,
        Guid deviceId)
    {
        return
            $"factoredge/{tenantId:D}/camera/{deviceId:D}/status";
    }

    private static CameraResponse Map(
        DeviceCamera camera,
        Domain.Entities.Device device)
    {
        return new CameraResponse
        {
            DeviceId = device.Id,
            DeviceCode = device.DeviceCode,
            Name = device.Name,
            StreamUrl = camera.StreamUrl,
            ContainerName = camera.ContainerName,
            OperatorStatus = camera.LastOperatorStatus,
            ConnectionStatus =
                device.ConnectionStatus.ToString(),
            LastStatusAtUtc = camera.LastStatusAtUtc,
            IsActive = device.IsActive
        };
    }
}
