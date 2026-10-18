namespace FactorEdge.Application.Features.Users.Models
{
    public sealed class TenantDashboardDto
    {
        public int TotalDevices { get; set; }
        public int TotalTelemetryRecords { get; set; }
        public int TotalEventLogs { get; set; }

        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }

        public ProductionEfficiencyDto Efficiency { get; set; } = new();
    }
}
