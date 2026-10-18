using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Auth.Models
{
    public sealed class LoginResponse
    {
        public Guid UserId { get; set; }
        public Guid? TenantId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
}
