using FactorEdge.Domain.Common;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Domain.Entities
{
    public class Device : BaseEntity<Guid>
    {
        public string DeviceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DeviceProtocol Protocol { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public byte? UnitId { get; set; }
        public string? RegisterMapJson { get; set; }
        public string? Topic { get; set; }
        public DeviceConnectionStatus ConnectionStatus { get; set; } = DeviceConnectionStatus.Disconnected;
        public DateTime? LastSeenUtc { get; set; }     
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAtUtc { get; set; }
        public int? LastMachineStatus { get; set; }
        public bool IsTemperatureAlarmActive { get; set; }
        public double? TemperatureAlarmCelsius { get; set; }
        public DateTime? TemperatureAlarmChangedAtUtc { get; set; }
        public DeviceType DeviceType { get; set; } = DeviceType.ProductionLine;
    }
}
