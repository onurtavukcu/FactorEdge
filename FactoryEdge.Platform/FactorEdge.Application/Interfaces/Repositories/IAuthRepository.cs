using FactorEdge.Domain.Entities;

namespace FactorEdge.Application.Interfaces.Repositories
{
    public interface IAuthRepository
    {
        Task<ApplicationUser?> GetUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
        Task<bool> IsTenantActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsTenantAdminActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }
}
