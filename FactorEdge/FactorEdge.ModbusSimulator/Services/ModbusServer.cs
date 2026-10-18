using FactorEdge.Contracts.Models;
using NModbus;
using NModbus.Data;
using System.Net;
using System.Net.Sockets;

namespace FactorEdge.ModbusSimulator.Services
{
    public sealed class ModbusServer : IDisposable
    {
        private readonly ILogger<ModbusServer> _logger;
        private readonly TcpListener _listener;
        private readonly IModbusSlaveNetwork _network;
        private readonly DefaultSlaveDataStore _dataStore = new();
        private readonly Guid _tenantId;
        private readonly string _deviceId;
        private readonly int _port;
        private readonly byte _unitId;

        public ModbusServer(ILogger<ModbusServer> logger, IConfiguration configuration)
        {
            _logger = logger;

            var tenantId = configuration.GetValue<Guid>("Simulator:TenantId");

            var deviceId = configuration["Simulator:DeviceId"];

            var port = configuration.GetValue<int>("Modbus:Port", 5020);

            var unitId = configuration.GetValue<byte>("Modbus:UnitId", 1);

            if (tenantId == Guid.Empty)
                throw new InvalidOperationException("Simulator TenantId missing.");

            if (string.IsNullOrWhiteSpace(deviceId))
                throw new InvalidOperationException("Simulator DeviceId missing.");

            if (port is < 1 or > 65535)
                throw new InvalidOperationException("Invalid Modbus port.");

            if (unitId == 0 || unitId > 247)
                throw new InvalidOperationException("Invalid Modbus UnitId.");

            _tenantId = tenantId;
            _deviceId = deviceId;
            _port = port;
            _unitId = unitId;

            var factory = new ModbusFactory();

            _listener = new TcpListener(IPAddress.Any, _port);

            _network = factory.CreateSlaveNetwork(_listener);

            var slave = factory.CreateSlave(_unitId, _dataStore);

            _logger.LogInformation(
                "Modbus Simulator configured | Tenant: {TenantId} | Device: {DeviceId}",
                _tenantId,
                _deviceId);

            _network.AddSlave(slave);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _listener.Start();

                _logger.LogInformation(
                      "Modbus TCP Server started | Tenant: {TenantId} | " +
                      "Device: {DeviceId} | Port: {Port} | UnitId: {UnitId}",
                      _tenantId,
                      _deviceId,
                      _port,
                      _unitId);

                await _network.ListenAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Uygulama normal şekilde kapatılıyor.
            }
            catch (SocketException ex)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(
                    ex,
                    "Modbus TCP listener stopped during shutdown.");
            }
            catch (ObjectDisposedException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Listener kapanış sırasında dispose edilmiş olabilir.
            }
            finally
            {
                _listener.Stop();

                _logger.LogInformation("Modbus TCP Server stopped.");
            }
        }

        public void UpdateRegisters(ProductionLineTelemetry data)
        {
            // 30001–30002: uint32, ABCD
            uint production = data.ProductionCount;

            _dataStore.InputRegisters.WritePoints(
                0,
                new ushort[]
                {
            (ushort)(production >> 16),
            (ushort)(production & 0xFFFF)
                });

            // 30003–30004: float32, CDAB
            uint temperatureBits =
                BitConverter.SingleToUInt32Bits(
                    (float)data.MotorTemperature);

            _dataStore.InputRegisters.WritePoints(
                2,
                new ushort[]
                {
            (ushort)(temperatureBits & 0xFFFF),
            (ushort)(temperatureBits >> 16)
                });

            // 30010: int16, ölçek 0.01
            short currentRaw = checked(
                (short)Math.Round(data.MotorCurrent * 100));

            _dataStore.InputRegisters.WritePoints(
                9,
                new ushort[]
                {
            unchecked((ushort)currentRaw)
                });

            // 30020: uint16
            _dataStore.InputRegisters.WritePoints(
                19,
                new ushort[]
                {
            checked((ushort)data.ScrapCount)
                });

            // 40001: uint16
            _dataStore.HoldingRegisters.WritePoints(
                0,
                new ushort[]
                {
            (ushort)data.Status
                });
        }

        public void Dispose()
        {
            _network.Dispose();
            _listener.Stop();
        }
    }
}