using FactorEdge.Contracts.Enums;
using FactorEdge.Contracts.Models;

namespace FactorEdge.MqttSimulator.Services
{
    public sealed class ProductionLineSimulator(IConfiguration configuration)
    {
        private readonly Random _random = new();

        private uint _productionCount;
        private uint _scrapCount;
        private double _temperature = 25;

        private ProductionLineStatus _status = ProductionLineStatus.Running;

        private readonly Guid _tenantId = GetTenantId(configuration);

        private readonly string _deviceId = GetDeviceId(configuration);

        private static Guid GetTenantId(IConfiguration configuration)
        {
            var tenantId = configuration.GetValue<Guid>("Simulator:TenantId");

            if (tenantId == Guid.Empty)
                throw new InvalidOperationException(
                    "Simulator TenantId missing.");

            return tenantId;
        }

        private static string GetDeviceId(IConfiguration configuration)
        {
            var deviceId = configuration["Simulator:DeviceId"];

            if (string.IsNullOrWhiteSpace(deviceId))
                throw new InvalidOperationException(
                    "Simulator DeviceId missing.");

            return deviceId;
        }
        public ProductionLineTelemetry Generate()
        {
            double current = 0;

            switch (_status)
            {
                case ProductionLineStatus.Running:

                    int produced = _random.Next(1, 5);

                    _productionCount += (uint)produced;
                    for (int i = 0; i < produced; i++)
                    {
                        if (_random.NextDouble() < 0.02)
                            _scrapCount++;
                    }

                    // Motor çalışma sıcaklığına yaklaşır.
                    _temperature += (70 - _temperature) * 0.08;
                     
                    _temperature += (_random.NextDouble() - 0.5) * 2;

                    current = 3 + _random.NextDouble() * 3;
                    break;

                case ProductionLineStatus.Stopped:

                    // Motor ortam sıcaklığına doğru soğur.
                    _temperature +=
                        (25 - _temperature) * 0.04;

                    current = 0;
                    break;

                case ProductionLineStatus.Fault:

                    _temperature += 5;

                    current = 7 + _random.NextDouble() * 3;
                    break;
            }

            return new ProductionLineTelemetry
            {
                TenantId = _tenantId,
                DeviceId = _deviceId,
                ProductionCount = _productionCount,
                ScrapCount = _scrapCount,
                MotorTemperature = Math.Round(_temperature, 2),
                MotorCurrent = Math.Round(current, 2),
                Status = _status,
                TimestampUtc = DateTime.UtcNow
            };
        }

        public void SetStatus(ProductionLineStatus status)
        {
            _status = status;
        }
    }
}