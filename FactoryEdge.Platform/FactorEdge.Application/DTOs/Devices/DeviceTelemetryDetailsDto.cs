namespace FactorEdge.Application.DTOs.Devices
{
    public sealed class DeviceTelemetryDetailsDto
    {
        public DeviceResponse Device { get; set; } = default!;
        public List<DeviceTelemetryItemDto> Telemetries { get; set; } = [];
        public DeviceTelemetryItemDto? Latest => Telemetries.FirstOrDefault();
        public List<DeviceEventLogDto> Events { get; set; } = [];
    }

    public sealed class DeviceTelemetryItemDto
    {
        public int ProductionCount { get; set; }
        public double MotorTemperature { get; set; }
        public double MotorCurrent { get; set; }
        public int ScrapCount { get; set; }
        public int Status { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
