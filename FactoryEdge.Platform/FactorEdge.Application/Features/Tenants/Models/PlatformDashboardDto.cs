namespace FactorEdge.Application.Features.Tenants.Models
{
    public sealed class PlatformDashboardDto
    {
        public int TotalTenants { get; set; }
        public int ActiveTenants { get; set; }
        public int SuspendedTenants { get; set; }
        public int FailedTenants { get; set; }
        public int ProvisioningTenants { get; set; }

        public int TotalUsers { get; set; }
        public int TenantAdmins { get; set; }
        public int RegularUsers { get; set; }
    }
}
