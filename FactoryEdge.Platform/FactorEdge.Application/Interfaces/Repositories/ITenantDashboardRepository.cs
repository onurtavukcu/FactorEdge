using FactorEdge.Application.Features.Users.Models;

namespace FactorEdge.Application.Interfaces.Repositories
{
    public interface ITenantDashboardRepository
    {
        Task<int> CountDevicesAsync(CancellationToken ct = default);
        Task<int> CountTelemetryAsync(CancellationToken ct = default);
        Task<int> CountEventLogsAsync(CancellationToken ct = default);
        Task<ProductionEfficiencyDto> GetEfficiencyLastHourAsync(CancellationToken ct = default);
    }
}
