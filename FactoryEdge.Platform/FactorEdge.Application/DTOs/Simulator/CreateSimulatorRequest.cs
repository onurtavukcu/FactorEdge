namespace FactorEdge.Application.DTOs.Simulator
{
    public sealed class CreateSimulatorRequest
    {
        public Guid TenantId { get; set; }
        public Guid DeviceId { get; set; }
        public string DeviceCode { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public byte? UnitId { get; set; }
    }
}
