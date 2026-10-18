using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Application.Features.Telemetry;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Device;
using FactorEdge.Contracts.Enums;
using FactorEdge.Contracts.Models;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Device.Services;

public sealed class DeviceTelemetryService(IDeviceRepository deviceRepository, IDeviceTelemetryRepository telemetryRepository, IDeviceEventLogService deviceEventLogService) : IDeviceTelemetryService
{
    public async Task SaveAsync(ProductionLineTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        // Transaction aç.
        await using var transaction = await deviceRepository.BeginTransactionAsync(cancellationToken);

        // Aynı cihaz için eşzamanlı işlemleri sıraya al.
        var device = await deviceRepository.GetTrackedByCodeForUpdateAsync(telemetry.DeviceId, cancellationToken);

        if (device is null)
            throw new KeyNotFoundException($"Device not found: {telemetry.DeviceId}");

        if (device.IsDeleted || !device.IsActive)
            return;

        int currentStatus = (int)telemetry.Status;

        if (!device.LastMachineStatus.HasValue)
        {
            await deviceEventLogService.AddAsync(
                device.Id,
                DeviceEventType.InitialStatusDetected,
                null,
                telemetry.Status.ToString(),
                $"Makinenin ilk durumu tespit edildi: {telemetry.Status}",
                cancellationToken);
        }
        else if (device.LastMachineStatus.Value != currentStatus)
        {
            string oldStatus = ((ProductionLineStatus)device.LastMachineStatus.Value).ToString();

            await deviceEventLogService.AddAsync(
                device.Id,
                DeviceEventType.MachineStatusChanged,
                oldStatus,
                telemetry.Status.ToString(),
                $"Makine durumu değişti: {oldStatus} → {telemetry.Status}",
                cancellationToken);
        }

        device.LastMachineStatus = currentStatus;

        var temperature = (double)telemetry.MotorTemperature;

        if (TemperatureAlarmPolicy.ShouldActivate(temperature, device.IsTemperatureAlarmActive))
        {
            device.IsTemperatureAlarmActive = true;
            device.TemperatureAlarmCelsius = temperature;
            device.TemperatureAlarmChangedAtUtc = DateTime.UtcNow;

            await deviceEventLogService.AddAsync(
                device.Id,
                DeviceEventType.TemperatureAlarmTriggered,
                null,
                temperature.ToString("F2",
                    System.Globalization.CultureInfo.InvariantCulture),
                $"Motor sıcaklığı kritik seviyeye ulaştı: {temperature:F2} °C.",
                cancellationToken);
        }
        else if (TemperatureAlarmPolicy.ShouldRecover(temperature, device.IsTemperatureAlarmActive))
        {
            device.IsTemperatureAlarmActive = false;
            device.TemperatureAlarmCelsius = temperature;
            device.TemperatureAlarmChangedAtUtc = DateTime.UtcNow;

            await deviceEventLogService.AddAsync(
                device.Id,
                DeviceEventType.TemperatureAlarmRecovered,
                "AlarmActive",
                "Normal",
                $"Motor sıcaklığı normale döndü: {temperature:F2} °C.",
                cancellationToken);
        }

        if (device.ConnectionStatus != DeviceConnectionStatus.Connected)
        {
            await deviceEventLogService.AddAsync(
                device.Id,
                DeviceEventType.ConnectionChanged,
                device.ConnectionStatus.ToString(),
                DeviceConnectionStatus.Connected.ToString(),
                "Cihazdan yeniden veri alınmaya başlandı.",
                cancellationToken);
        }

        device.ConnectionStatus = DeviceConnectionStatus.Connected;

        DateTime now = DateTime.UtcNow;

        // Son görülme zamanı geriye gitmesin.
        if (!device.LastSeenUtc.HasValue || device.LastSeenUtc.Value < now)
        {
            device.LastSeenUtc = now;
        }

        var entity = new DeviceTelemetry
        {
            DeviceId = device.Id,
            ProductionCount = telemetry.ProductionCount,
            MotorTemperature = telemetry.MotorTemperature,
            MotorCurrent = telemetry.MotorCurrent,
            ScrapCount = telemetry.ScrapCount,
            Status = currentStatus,
            TimestampUtc = telemetry.TimestampUtc,
            ReceivedAtUtc = now
        };

        await telemetryRepository.AddAsync(entity, cancellationToken);

        // Telemetry, Device ve EventLog aynı transaction içinde.
        await telemetryRepository.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<List<DeviceTelemetryItemDto>> GetHistoryAsync(Guid deviceId, CancellationToken cancellationToken = default) 
    {
        var device = await deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null || device.IsDeleted)
            throw new KeyNotFoundException("Cihaz bulunamadı.");

        var now = DateTime.UtcNow;

        var records = await telemetryRepository.GetHistoryAsync(deviceId, now.AddHours(-1), now, cancellationToken);

        return records.Select(x => new DeviceTelemetryItemDto
        {
            ProductionCount = Convert.ToInt32(x.ProductionCount),
            MotorTemperature = Convert.ToDouble(x.MotorTemperature),
            MotorCurrent = Convert.ToDouble(x.MotorCurrent),
            ScrapCount = Convert.ToInt32(x.ScrapCount),
            Status = x.Status,
            TimestampUtc = x.TimestampUtc
        }).ToList();
    }

    public async Task<List<DeviceTelemetryItemDto>> GetRecentAsync(Guid deviceId, int take = 50, CancellationToken cancellationToken = default)
    {
        var device = await deviceRepository.GetByIdAsync(deviceId, cancellationToken);

        if (device is null || device.IsDeleted)
            throw new KeyNotFoundException("Cihaz bulunamadı.");

        var records = await telemetryRepository.GetRecentByDeviceIdAsync(deviceId, take, cancellationToken);

        return records.Select(x => new DeviceTelemetryItemDto
        {
            ProductionCount = Convert.ToInt32(x.ProductionCount),
            MotorTemperature = Convert.ToDouble(x.MotorTemperature),
            MotorCurrent = Convert.ToDouble(x.MotorCurrent),
            ScrapCount = Convert.ToInt32(x.ScrapCount),
            Status = x.Status,
            TimestampUtc = x.TimestampUtc
        }).ToList();
    }
}
