using FactorEdge.Application.DTOs.Simulator;

namespace FactorEdge.Application.Interfaces.Services.Simulator
{
    public interface ISimulatorProvisioningService
    {
        Task<SimulatorProvisionResult> CreateAsync(CreateSimulatorRequest request, CancellationToken cancellationToken = default);

        Task SoftDeleteAsync(Guid tenantId, Guid deviceId, CancellationToken cancellationToken = default);
    }
}
