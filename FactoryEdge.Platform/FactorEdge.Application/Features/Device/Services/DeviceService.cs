using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Application.DTOs.Simulator;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Application.Interfaces.Services.Simulator;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Device.Services
{
    public sealed class DeviceService(IDeviceRepository deviceRepository, IDeviceEventLogService deviceEventLogService, ISimulatorProvisioningService simulatorProvisioningService) : IDeviceService
    {

        public async Task<Guid> CreateAsync(
            Guid tenantId,
            CreateDeviceRequest request,
            CancellationToken cancellationToken = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.");

            if (string.IsNullOrWhiteSpace(request.DeviceCode))
                throw new ArgumentException("DeviceCode is required.");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Device name is required.");

            if (!Enum.IsDefined(request.Protocol))
                throw new ArgumentException("Invalid device protocol.");

            var deviceCode = request.DeviceCode.Trim();

            if (!System.Text.RegularExpressions.Regex.IsMatch(
                deviceCode,
                @"^[a-zA-Z0-9_-]{1,64}$"))
            {
                throw new ArgumentException(
                    "DeviceCode may contain only letters, numbers, '_' and '-'.");
            }

            if (request.Protocol == DeviceProtocol.ModbusTcp &&
                (request.UnitId is null or < 1 or > 247))
            {
                throw new ArgumentException(
                    "Modbus UnitId must be between 1 and 247.");
            }

            var existing = await deviceRepository.GetByCodeAsync(
                deviceCode,
                cancellationToken);

            if (existing is not null)
                throw new InvalidOperationException(
                    "A device with this code already exists.");

            var deviceId = Guid.NewGuid();

            var provisionRequest = new CreateSimulatorRequest
            {
                TenantId = tenantId,
                DeviceId = deviceId,
                DeviceCode = deviceCode,
                Protocol = request.Protocol.ToString(),
                UnitId = request.Protocol == DeviceProtocol.ModbusTcp
                    ? request.UnitId
                    : null
            };

            // 1. Ubuntu üzerinde Docker container oluştur.
            var provisionResult =
                await simulatorProvisioningService.CreateAsync(
                    provisionRequest,
                    cancellationToken);

            try
            {
                // 2. Docker'ın döndürdüğü bağlantı bilgileriyle
                // tenant veritabanına cihazı ekle.
                var device = new FactorEdge.Domain.Entities.Device
                {
                    Id = deviceId,
                    DeviceCode = deviceCode,
                    Name = request.Name.Trim(),
                    Protocol = request.Protocol,
                    Host = provisionResult.Host,
                    Port = provisionResult.Port,
                    UnitId = request.Protocol == DeviceProtocol.ModbusTcp
                        ? provisionResult.UnitId
                        : null,
                    RegisterMapJson =
                        request.Protocol == DeviceProtocol.ModbusTcp
                            ? request.RegisterMapJson
                            : null,
                    Topic = request.Protocol == DeviceProtocol.Mqtt
                        ? provisionResult.Topic
                        : null,
                    IsActive = true,
                    ConnectionStatus = DeviceConnectionStatus.Disconnected
                };

                await deviceRepository.AddAsync(
                    device,
                    cancellationToken);

                await deviceRepository.SaveChangesAsync(
                    cancellationToken);

                return device.Id;
            }
            catch
            {
                // Docker container oluşturuldu fakat DB kaydı
                // başarısız oldu. Sahipsiz container bırakma.
                try
                {
                    await simulatorProvisioningService.SoftDeleteAsync(
                        tenantId,
                        deviceId,
                        CancellationToken.None);
                }
                catch
                {
                    // Temizlik de başarısızsa orijinal hata
                    // korunur. Loglama/uzlaştırma sonraki adımdır.
                }

                throw;
            }
        }


        public async Task<List<DeviceResponse>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var devices = await deviceRepository.GetAllAsync(cancellationToken);

            return devices
                .Where(x => x.DeviceType == DeviceType.ProductionLine)
                .Select(x => new DeviceResponse
                {
                    Id = x.Id,
                    DeviceCode = x.DeviceCode,
                    Name = x.Name,
                    Protocol = x.Protocol,
                    Host = x.Host,
                    Port = x.Port,
                    IsActive = x.IsActive,
                    ConnectionStatus = x.ConnectionStatus,
                    LastSeenUtc = x.LastSeenUtc
                })
                .ToList();
        }

        public async Task<UpdateDeviceRequest?> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var device = await deviceRepository.GetByIdAsync(id, cancellationToken);

            if (device is null)
                return null;

            return new UpdateDeviceRequest
            {
                Id = device.Id,
                DeviceCode = device.DeviceCode,
                Name = device.Name,
                Protocol = device.Protocol,
                Host = device.Host,
                Port = device.Port,
                UnitId = device.UnitId,
                RegisterMapJson = device.RegisterMapJson,
                Topic = device.Topic,
                IsActive = device.IsActive
            };
        }


        public async Task UpdateAsync(UpdateDeviceRequest request, CancellationToken cancellationToken = default)
        {
            var device = await deviceRepository.GetTrackedByIdAsync(request.Id, cancellationToken);

            if (device is null)
                throw new KeyNotFoundException("Device not found.");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Device name is required.");

            if (string.IsNullOrWhiteSpace(request.Host))
                throw new ArgumentException("Host is required.");

            if (request.Port is < 1 or > 65535)
                throw new ArgumentException("Invalid port.");

            if (!Enum.IsDefined(request.Protocol))
                throw new ArgumentException("Invalid protocol.");

            if (request.Protocol == DeviceProtocol.ModbusTcp &&
                request.UnitId is null)
                throw new ArgumentException("Modbus UnitId is required.");

            if (request.Protocol == DeviceProtocol.Mqtt &&
                string.IsNullOrWhiteSpace(request.Topic))
                throw new ArgumentException("MQTT Topic is required.");

            // 1. Güncellemeden önceki durumları sakla
            bool wasActive = device.IsActive;

            var oldConnectionStatus = device.ConnectionStatus;

            // 2. Bağlantı ayarları değişmiş mi?
            bool connectionSettingsChanged =
                device.Protocol != request.Protocol ||
                device.Host != request.Host.Trim() ||
                device.Port != request.Port ||
                device.UnitId != (
                    request.Protocol == DeviceProtocol.ModbusTcp
                        ? request.UnitId
                        : null) ||
                device.Topic != (
                    request.Protocol == DeviceProtocol.Mqtt
                        ? request.Topic
                        : null) ||
                device.RegisterMapJson != (
                    request.Protocol == DeviceProtocol.ModbusTcp
                        ? request.RegisterMapJson
                        : null);

            // 3. Cihaz bilgilerini güncelle
            device.Name = request.Name.Trim();
            device.Protocol = request.Protocol;
            device.Host = request.Host.Trim();
            device.Port = request.Port;

            device.UnitId = request.Protocol == DeviceProtocol.ModbusTcp
                ? request.UnitId
                : null;

            device.RegisterMapJson = request.Protocol == DeviceProtocol.ModbusTcp
                ? request.RegisterMapJson
                : null;

            device.Topic = request.Protocol == DeviceProtocol.Mqtt
                ? request.Topic
                : null;

            device.IsActive = request.IsActive;

            // 4. Aktiflik durumu değiştiyse log oluştur
            if (wasActive != device.IsActive)
            {
                await deviceEventLogService.AddAsync(
                    device.Id,
                    device.IsActive
                        ? DeviceEventType.DeviceActivated
                        : DeviceEventType.DeviceDeactivated,
                    wasActive.ToString(),
                    device.IsActive.ToString(),
                    device.IsActive
                        ? "Cihaz kullanıcı tarafından aktif edildi."
                        : "Cihaz kullanıcı tarafından pasifleştirildi.",
                    cancellationToken);
            }

            // 5. Bağlantı ayarları değiştiyse veya cihaz
            // pasifleştirildiyse Disconnected yap
            if (connectionSettingsChanged || !device.IsActive)
            {
                if (oldConnectionStatus !=
                    DeviceConnectionStatus.Disconnected)
                {
                    await deviceEventLogService.AddAsync(
                        device.Id,
                        DeviceEventType.ConnectionChanged,
                        oldConnectionStatus.ToString(),
                        DeviceConnectionStatus.Disconnected.ToString(),
                        !device.IsActive
                            ? "Cihaz pasifleştirildiği için bağlantı durumu kapatıldı."
                            : "Cihaz bağlantı ayarları değiştirildi.",
                        cancellationToken);

                    device.ConnectionStatus =
                        DeviceConnectionStatus.Disconnected;
                }
            }

            // 6. Bütün değişiklikleri tek seferde kaydet
            await deviceRepository.SaveChangesAsync(cancellationToken);
        }


        public async Task SoftDeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.");

            var device = await deviceRepository.GetTrackedByIdAsync(
                id,
                cancellationToken);

            if (device is null || device.IsDeleted)
                throw new KeyNotFoundException("Device not found.");

            bool wasActive = device.IsActive;
            var previousConnectionStatus = device.ConnectionStatus;

            // 1. Önce Docker container'ını sil.
            await simulatorProvisioningService.SoftDeleteAsync(
                tenantId,
                device.Id,
                cancellationToken);

            // 2. Sonra cihazı veritabanında arşivle.
            device.IsDeleted = true;
            device.IsActive = false;
            device.DeletedAtUtc = DateTime.UtcNow;
            device.ConnectionStatus = DeviceConnectionStatus.Disconnected;

            await deviceEventLogService.AddAsync(
                device.Id,
                DeviceEventType.DeviceSoftDeleted,
                "False",
                "True",
                "Cihaz arşive taşındı ve Docker simülatörü silindi.",
                cancellationToken);

            if (wasActive)
            {
                await deviceEventLogService.AddAsync(
                    device.Id,
                    DeviceEventType.DeviceDeactivated,
                    "True",
                    "False",
                    "Cihaz silindiği için pasifleştirildi.",
                    cancellationToken);
            }

            if (previousConnectionStatus != device.ConnectionStatus)
            {
                await deviceEventLogService.AddAsync(
                    device.Id,
                    DeviceEventType.ConnectionChanged,
                    previousConnectionStatus.ToString(),
                    device.ConnectionStatus.ToString(),
                    "Cihaz silindiği için bağlantı durumu kapatıldı.",
                    cancellationToken);
            }

            // 3. Değişiklikleri DB'ye kaydet.
            await deviceRepository.SaveChangesAsync(cancellationToken);
        }

        public async Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var device = await deviceRepository.GetTrackedByIdAsync(id, cancellationToken);

            if (device is null)
                throw new KeyNotFoundException("Device not found.");

            deviceRepository.HardDelete(device);

            await deviceRepository.SaveChangesAsync(cancellationToken);
        }
    }
}
