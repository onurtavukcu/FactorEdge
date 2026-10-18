using FactorEdge.Application.Features.Tenants.Models;

namespace FactorEdge.Application.Interfaces.Services.Tenants
{
    public interface ITenantManagementService
    {
        Task<IReadOnlyList<TenantListDto>> GetAllAsync(bool onlyActive = true, CancellationToken cancellationToken = default);
        Task<TenantDetailsDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<TenantEditDto?> GetForEditAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task UpdateAsync(UpdateTenantRequest request, CancellationToken cancellationToken = default);
        Task<PlatformDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    }
}
