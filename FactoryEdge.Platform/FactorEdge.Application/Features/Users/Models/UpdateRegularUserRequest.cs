using System.ComponentModel.DataAnnotations;

namespace FactorEdge.Application.Features.Users.Models
{
    public sealed class UpdateRegularUserRequest
    {
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
