using FactorEdge.Domain.Common;

namespace FactorEdge.Domain.Entities
{
    public class DeviceTelemetry : BaseEntity<long>
    {
        public Guid DeviceId { get; set; }
        public Device Device { get; set; } = null!;
        public uint ProductionCount { get; set; }
        public double MotorTemperature { get; set; }
        public double MotorCurrent { get; set; }
        public uint ScrapCount { get; set; }
        public int Status { get; set; }
        public DateTime TimestampUtc { get; set; }
        public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
