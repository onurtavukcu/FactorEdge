using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.DTOs.Devices
{
    public class UpdateDeviceRequest
    {
        public Guid Id { get; set; }
        public string DeviceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DeviceProtocol Protocol { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public byte? UnitId { get; set; }
        public string? RegisterMapJson { get; set; }
        public string? Topic { get; set; }
        public bool IsActive { get; set; }
    }
}
