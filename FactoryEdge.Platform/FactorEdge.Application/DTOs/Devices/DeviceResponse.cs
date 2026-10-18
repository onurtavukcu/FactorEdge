using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.DTOs.Devices
{
    public sealed class DeviceResponse
    {
        public Guid Id { get; set; }
        public string DeviceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DeviceProtocol Protocol { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool IsActive { get; set; }
        public DeviceConnectionStatus ConnectionStatus { get; set; }
        public DateTime? LastSeenUtc { get; set; }
    }
}
