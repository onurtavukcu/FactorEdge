using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.DTOs.Devices
{
    public sealed class CreateDeviceRequest
    {
        public string DeviceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DeviceProtocol Protocol { get; set; }

        // Modbus cihazları için
        public byte? UnitId { get; set; }
        public string? RegisterMapJson { get; set; }
    }
}
