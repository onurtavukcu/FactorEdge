using FactorEdge.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FactorEdge.Application.Features.Tenants.Models
{
    public class TenantEditDto
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Şirket adı zorunludur.")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public TenantStatus Status { get; set; }

        [Required(ErrorMessage = "Yönetici adı zorunludur.")]
        [MaxLength(100)]
        public string AdminUserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yönetici e-postası zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
        [MaxLength(256)]
        public string AdminEmail { get; set; } = string.Empty;

        public bool AdminIsActive { get; set; }
    }
}
