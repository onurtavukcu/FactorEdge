using FactorEdge.Contracts.Enums;

namespace FactorEdge.Contracts.Models
{
    public sealed class ProductionLineTelemetry
    {
        public Guid TenantId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public uint ProductionCount { get; set; }
        public double MotorTemperature { get; set; }
        public double MotorCurrent { get; set; }
        public uint ScrapCount { get; set; }
        public ProductionLineStatus Status { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
