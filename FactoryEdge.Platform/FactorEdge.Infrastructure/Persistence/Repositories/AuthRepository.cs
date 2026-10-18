using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace FactorEdge.Infrastructure.Persistence.Repositories
{
    public sealed class AuthRepository(BaseDbContext dbContext) : IAuthRepository
    {
        public Task<ApplicationUser?> GetUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            return dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.NormalizedEmail == normalizedEmail,
                    cancellationToken);
        }

        public Task<bool> IsTenantActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return dbContext.Tenants
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == tenantId &&
                         x.Status == TenantStatus.Active,
                    cancellationToken);
        }

        public Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == userId,
                    cancellationToken);
        }

        public Task<bool> IsTenantAdminActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.TenantId == tenantId &&
                         x.Role == UserRole.TenantAdmin &&
                         x.IsActive,
                    cancellationToken);
        }
    }
}
