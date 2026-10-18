using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Interfaces.Repositories
{
    public interface IPlatformRepository
    {
        Task AddTenantAsync(Tenant tenant, CancellationToken ct = default);        
        Task<List<Tenant>> GetTenantsAsync(bool onlyActive, CancellationToken ct = default);
        Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task<ApplicationUser?> GetTenantAdminAsync(Guid tenantId, CancellationToken ct = default);
        Task<List<ApplicationUser>> GetRegularUsersAsync(Guid tenantId, CancellationToken ct = default);
        Task<ApplicationUser?> GetRegularUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
        void AddUser(ApplicationUser user);

        // Common controls
        Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludeUserId = null, CancellationToken ct = default);
        Task<bool> IsTenantActiveAsync(Guid tenantId, CancellationToken ct = default);

        Task<int> CountUsersAsync(Guid tenantId, bool? isActive = null, CancellationToken ct = default);
        Task<int> CountAllTenantsAsync(CancellationToken ct = default);
        Task<int> CountTenantsByStatusAsync(TenantStatus status, CancellationToken ct = default);
        Task<int> CountAllUsersAsync(CancellationToken ct = default);
        Task<int> CountUsersByRoleAsync(UserRole role, CancellationToken ct = default);

        Task<string?> GetActiveTenantDatabaseNameAsync(Guid tenantId, CancellationToken ct = default);
        Task<List<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
