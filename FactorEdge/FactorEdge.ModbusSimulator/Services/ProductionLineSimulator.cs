using FactorEdge.Contracts.Enums;
using FactorEdge.Contracts.Models;

namespace FactorEdge.ModbusSimulator.Services
{
    public sealed class ProductionLineSimulator
    {
        private readonly Random _random = new();

        private uint _productionCount;
        private uint _scrapCount;
        private double _temperature = 25;

        private ProductionLineStatus _status =
            ProductionLineStatus.Running;

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

                    _temperature += (70 - _temperature) * 0.08;
                    _temperature += (_random.NextDouble() - 0.5) * 2;

                    current = 3 + _random.NextDouble() * 3;
                    break;

                case ProductionLineStatus.Stopped:
                    _temperature += (25 - _temperature) * 0.04;
                    current = 0;
                    break;

                case ProductionLineStatus.Fault:
                    _temperature += 5;
                    current = 7 + _random.NextDouble() * 3;
                    break;
            }

            return new ProductionLineTelemetry
            {
                DeviceId = "machine-01",
                ProductionCount = _productionCount,
                MotorTemperature = Math.Round(_temperature, 2),
                MotorCurrent = Math.Round(current, 2),
                ScrapCount = _scrapCount,
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
