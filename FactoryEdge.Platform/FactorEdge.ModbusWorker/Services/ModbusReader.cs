
using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Contracts.Enums;
using FactorEdge.Contracts.Models;
using NModbus;
using System.Net.Sockets;

namespace FactorEdge.ModbusWorker.Services;

public sealed class ModbusReader(IConfiguration configuration)
{
    public async Task<ProductionLineTelemetry> ReadAsync(
        DeviceConnectionDto device,
        CancellationToken cancellationToken)
    {
        if (device.UnitId is null)
            throw new InvalidOperationException(
                $"Modbus UnitId missing: {device.DeviceCode}");

        if (string.IsNullOrWhiteSpace(device.Host))
            throw new InvalidOperationException(
                $"Modbus Host missing: {device.DeviceCode}");

        if (device.Port is < 1 or > 65535)
            throw new InvalidOperationException(
                $"Invalid Modbus Port: {device.DeviceCode}");

        int connectionTimeoutMs =
            configuration.GetValue<int>(
                "Modbus:ConnectionTimeoutMs", 3000);

        int readTimeoutMs =
            configuration.GetValue<int>(
                "Modbus:ReadTimeoutMs", 3000);

        if (connectionTimeoutMs <= 0 || readTimeoutMs <= 0)
            throw new InvalidOperationException(
                "Modbus timeout values must be greater than zero.");

        string host = device.Host;
        int port = device.Port;
        byte unitId = device.UnitId.Value;

        using var client = new TcpClient
        {
            ReceiveTimeout = readTimeoutMs,
            SendTimeout = readTimeoutMs
        };

        // 1. TCP bağlantısı için ayrı timeout
        using (var connectCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken))
        {
            connectCts.CancelAfter(connectionTimeoutMs);

            try
            {
                await client.ConnectAsync(
                    host,
                    port,
                    connectCts.Token);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Modbus connection timeout: {device.DeviceCode} " +
                    $"({host}:{port}) after {connectionTimeoutMs}ms.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 2. NModbus bağlantısı
        var factory = new ModbusFactory();
        using var master = factory.CreateMaster(client);

        // NModbus'un kendi taşıma katmanı timeout'ları
        master.Transport.ReadTimeout = readTimeoutMs;
        master.Transport.WriteTimeout = readTimeoutMs;

        cancellationToken.ThrowIfCancellationRequested();

        // 30001-30004: Production + Temperature
        ushort[] input = master.ReadInputRegisters(
            unitId, 0, 4);

        cancellationToken.ThrowIfCancellationRequested();

        // uint32 ABCD
        uint production =
            ((uint)input[0] << 16) | input[1];

        // float32 CDAB
        uint temperatureBits =
            ((uint)input[3] << 16) | input[2];

        float temperature =
            BitConverter.UInt32BitsToSingle(temperatureBits);

        // 30010: Motor Current
        ushort[] currentRegisters =
            master.ReadInputRegisters(unitId, 9, 1);

        cancellationToken.ThrowIfCancellationRequested();

        short currentRaw =
            unchecked((short)currentRegisters[0]);

        double current = currentRaw * 0.01;

        // 30020: Scrap
        ushort[] scrapRegisters =
            master.ReadInputRegisters(unitId, 19, 1);

        cancellationToken.ThrowIfCancellationRequested();

        // 40001: Status
        ushort[] statusRegisters =
            master.ReadHoldingRegisters(unitId, 0, 1);

        cancellationToken.ThrowIfCancellationRequested();

        return new ProductionLineTelemetry
        {
            DeviceId = device.DeviceCode,
            ProductionCount = production,
            MotorTemperature = Math.Round(temperature, 2),
            MotorCurrent = Math.Round(current, 2),
            ScrapCount = scrapRegisters[0],
            Status = (ProductionLineStatus)statusRegisters[0],
            TimestampUtc = DateTime.UtcNow
        };
    }
}
