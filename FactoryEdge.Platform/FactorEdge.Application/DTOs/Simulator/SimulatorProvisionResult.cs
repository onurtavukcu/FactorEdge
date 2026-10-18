namespace FactorEdge.Application.DTOs.Simulator
{
    public sealed class SimulatorProvisionResult
    {
        public string ContainerName { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? Topic { get; set; }
        public byte? UnitId { get; set; }
    }
}
