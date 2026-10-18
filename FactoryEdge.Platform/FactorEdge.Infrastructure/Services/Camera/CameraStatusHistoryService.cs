
using FactorEdge.Application.DTOs.Cameras;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Camera;
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactorEdge.Infrastructure.Services.Camera;

public sealed class CameraStatusHistoryService(IServiceScopeFactory scopeFactory) : ICameraStatusHistoryService
{
    public async Task<List<CameraStatusHistoryResponse>> GetRecentAsync(Guid tenantId, int count = 50, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Geçersiz TenantId.");

        count = Math.Clamp(count, 1, 100);

        using var scope = scopeFactory.CreateScope();

        var platformRepository = scope.ServiceProvider.GetRequiredService<IPlatformRepository>();

        var databaseName = await platformRepository.GetActiveTenantDatabaseNameAsync(tenantId, cancellationToken);

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Tenant aktif değil.");

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.SetTenant(databaseName);

        // Tenant context ayarlandıktan sonra DbContext alıyoruz.
        var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        return await db.CameraStatusHistories
            .AsNoTracking()
            .Where(x =>
                x.Device.DeviceType == DeviceType.Camera &&
                !x.Device.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(count)
            .Select(x => new CameraStatusHistoryResponse
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceCode = x.Device.DeviceCode,
                CameraName = x.Device.Name,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }
}
