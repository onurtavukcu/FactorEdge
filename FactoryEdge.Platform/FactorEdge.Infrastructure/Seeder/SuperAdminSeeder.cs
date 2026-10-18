
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using FactorEdge.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactorEdge.Infrastructure.Seeder
{
    public sealed class SuperAdminSeeder(BaseDbContext dbContext, IPasswordHasher<ApplicationUser> passwordHasher, IOptions<SuperAdminSettings> options)
    {
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var settings = options.Value;

            if (string.IsNullOrWhiteSpace(settings.Email))
                throw new InvalidOperationException("SuperAdmin email is missing.");

            var email = settings.Email.Trim();
            var normalizedEmail = email.ToUpperInvariant();

            var exists = await dbContext.Users.AnyAsync(x => x.Role == UserRole.SuperAdmin,
                cancellationToken);

            if (exists)
                return;


            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                UserName = settings.UserName.Trim(),
                Email = email,
                NormalizedEmail = normalizedEmail,
                Role = UserRole.SuperAdmin,
                IsActive = true
            };

            if (string.IsNullOrWhiteSpace(settings.Password))
                throw new InvalidOperationException("SuperAdmin password is missing.");

            admin.PasswordHash = passwordHasher.HashPassword(admin, settings.Password);


            dbContext.Users.Add(admin);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
