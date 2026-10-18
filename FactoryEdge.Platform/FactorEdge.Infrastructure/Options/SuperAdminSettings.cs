namespace FactorEdge.Infrastructure.Options
{
    public class SuperAdminSettings
    {
        public const string SectionName = "SuperAdmin";
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
