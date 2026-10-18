using FactorEdge.Domain.Common;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Domain.Entities
{
    public class ApplicationUser : BaseEntity<Guid>
    {
        public Guid? TenantId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.RegularUser;
        public bool IsActive { get; set; } = true;
    }
}
