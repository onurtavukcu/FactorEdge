using FactorEdge.Application.Features.Tenants.Models;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Tenants;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FactorEdge.Application.Features.Tenants.Services;

public sealed class TenantProvisioningService(IPlatformRepository platformRepository, ITenantProvisioningRepository provisioningRepository, ILogger<TenantProvisioningService> logger) : ITenantProvisioningService
{
    public async Task<CreateTenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AdminUserName) || string.IsNullOrWhiteSpace(request.AdminEmail) || string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            throw new ArgumentException("Tenant admin credentials are required.");
        }

        var normalizedEmail = request.AdminEmail.Trim().ToUpperInvariant();

        var emailExists = await platformRepository.EmailExistsAsync(normalizedEmail, ct: cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
        }

        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name.Trim(),
            DatabaseName = $"factoredge_tenant_{tenantId:N}",
            Status = TenantStatus.Provisioning
        };

        await platformRepository.AddTenantAsync(tenant, cancellationToken);

        await platformRepository.SaveChangesAsync(cancellationToken);

        bool databaseCreated = false;

        try
        {
            databaseCreated = await provisioningRepository.CreateDatabaseAsync(tenant.DatabaseName, cancellationToken);

            logger.LogInformation("Tenant database created: {DatabaseName}", tenant.DatabaseName);

            await provisioningRepository.MigrateDatabaseAsync(tenant.DatabaseName, cancellationToken);

            await provisioningRepository.SeedTenantAdminAsync(tenant.Id, request.AdminUserName, request.AdminEmail, request.AdminPassword, cancellationToken);

            tenant.Status = TenantStatus.Active;
            tenant.ProvisioningError = null;

            await platformRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tenant provisioning failed: {TenantId}", tenant.Id);

            tenant.Status = TenantStatus.Failed;
            tenant.ProvisioningError = ex.Message;

            try
            {
                await platformRepository.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception statusEx)
            {
                logger.LogError(statusEx, "Failed to save tenant failure status: {TenantId}", tenant.Id);
            }

            if (databaseCreated)
            {
                try
                {
                    await provisioningRepository.DeleteDatabaseAsync(tenant.DatabaseName, CancellationToken.None);

                    logger.LogInformation("Failed tenant database deleted: {DatabaseName}", tenant.DatabaseName);
                }
                catch (Exception cleanupEx)
                {
                    logger.LogError(cleanupEx, "Tenant database cleanup failed: {DatabaseName}", tenant.DatabaseName);
                }
            }

            throw;
        }

        return new CreateTenantResponse
        {
            TenantId = tenant.Id,
            Name = tenant.Name,
            DatabaseName = tenant.DatabaseName,
            Status = tenant.Status.ToString()
        };
    }
}

