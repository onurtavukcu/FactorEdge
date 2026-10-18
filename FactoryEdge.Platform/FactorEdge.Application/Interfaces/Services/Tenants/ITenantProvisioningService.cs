using FactorEdge.Application.Features.Tenants.Models;

namespace FactorEdge.Application.Interfaces.Services.Tenants
{
    public interface ITenantProvisioningService
    {
        Task<CreateTenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default);
    }
}
