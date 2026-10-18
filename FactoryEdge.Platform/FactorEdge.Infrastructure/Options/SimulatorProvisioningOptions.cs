namespace FactorEdge.Infrastructure.Options
{
    public sealed class SimulatorProvisioningOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string PrivateKeyPath { get; set; } = string.Empty;
        public string ScriptPath { get; set; } = string.Empty;
        public int MqttPort { get; set; } = 1883;
        public string HostKeySha256 { get; set; } = string.Empty;
    }
}
