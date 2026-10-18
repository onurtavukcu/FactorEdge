using FactorEdge.Application.Features.Users.Models;

namespace FactorEdge.Application.Interfaces.Services.Users
{
    public interface IUserManagementService
    {
        Task<IReadOnlyList<RegularUserDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<RegularUserDto?> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
        Task CreateAsync(Guid tenantId, CreateRegularUserRequest request, CancellationToken cancellationToken = default);
        Task UpdateAsync(Guid tenantId, UpdateRegularUserRequest request, CancellationToken cancellationToken = default);
        Task<TenantDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }
}