using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Seeder
{
    public sealed class TenantAdminSeeder(BaseDbContext context)
    {
        public async Task SeedAsync(Guid tenantId, string userName, string email, string password, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var existing = await context.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

            if (existing)
                throw new InvalidOperationException(
                    "Bu e-posta adresi zaten kullanılıyor.");

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserName = userName.Trim(),
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                Role = UserRole.TenantAdmin,
                IsActive = true
            };

            var passwordHasher = new PasswordHasher<ApplicationUser>();

            user.PasswordHash = passwordHasher.HashPassword(user, password);

            context.Users.Add(user);

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
