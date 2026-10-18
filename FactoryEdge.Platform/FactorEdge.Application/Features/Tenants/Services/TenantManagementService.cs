
using System.ComponentModel.DataAnnotations;
using FactorEdge.Application.Features.Tenants.Models;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Tenants;
using FactorEdge.Domain.Enums;

namespace FactorEdge.Application.Features.Tenants.Services;

public sealed class TenantManagementService(IPlatformRepository repository) : ITenantManagementService
{
    public async Task<IReadOnlyList<TenantListDto>> GetAllAsync(bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var tenants = await repository.GetTenantsAsync(onlyActive, cancellationToken); 

        return tenants.Select(x => new TenantListDto
        {
            Id = x.Id,
            Name = x.Name,
            DatabaseName = x.DatabaseName,
            Status = x.Status,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    public async Task<TenantDetailsDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await repository.GetTenantAsync(tenantId, cancellationToken);

        if (tenant is null)
            return null;

        var userCount = await repository.CountUsersAsync(tenantId, ct: cancellationToken);

        return new TenantDetailsDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            DatabaseName = tenant.DatabaseName,
            Status = tenant.Status,
            CreatedAtUtc = tenant.CreatedAtUtc,
            ProvisioningError = tenant.ProvisioningError,
            UserCount = userCount
        };
    }

    public async Task<TenantEditDto?> GetForEditAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await repository.GetTenantAsync(tenantId, cancellationToken);

        if (tenant is null)
            return null;

        if (tenant.Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            throw new InvalidOperationException("Bu tenant normal düzenleme işlemine uygun değil.");
        }

        var admin = await repository.GetTenantAdminAsync(tenantId, cancellationToken);

        if (admin is null)
            throw new InvalidOperationException("TenantAdmin hesabı bulunamadı.");

        return new TenantEditDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Status = tenant.Status,
            AdminUserName = admin.UserName,
            AdminEmail = admin.Email,
            AdminIsActive = admin.IsActive
        };
    }

    public async Task UpdateAsync(UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("Geçersiz TenantId.");

        var name = request.Name?.Trim() ?? "";
        var adminUserName = request.AdminUserName?.Trim() ?? "";
        var email = request.AdminEmail?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name) ||
            name.Length > 200 ||
            string.IsNullOrWhiteSpace(adminUserName) ||
            adminUserName.Length > 100 ||
            string.IsNullOrWhiteSpace(email) ||
            email.Length > 256 ||
            !new EmailAddressAttribute().IsValid(email))
        {
            throw new ArgumentException("Tenant veya yönetici bilgileri geçersiz.");
        }

        if (request.Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            throw new InvalidOperationException("Yalnızca Active ve Suspended durumları seçilebilir.");
        }

        var tenant = await repository.GetTenantAsync(request.TenantId, cancellationToken);

        if (tenant is null)
            throw new KeyNotFoundException("Tenant bulunamadı.");

        if (tenant.Status is not (TenantStatus.Active or TenantStatus.Suspended))
        {
            throw new InvalidOperationException("Bu tenant'ın durumu normal düzenleme ekranından değiştirilemez.");
        }

        var admin = await repository.GetTenantAdminAsync(tenant.Id, cancellationToken);

        if (admin is null)
            throw new InvalidOperationException("TenantAdmin hesabı bulunamadı.");

        var normalizedEmail = email.ToUpperInvariant();

        var emailExists = await repository.EmailExistsAsync(normalizedEmail, admin.Id, cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException("Bu e-posta adresi başka bir kullanıcı tarafından kullanılıyor.");
        }

        tenant.Name = name;
        tenant.Status = request.AdminIsActive
            ? TenantStatus.Active
            : TenantStatus.Suspended;

        admin.UserName = adminUserName;
        admin.Email = email;
        admin.NormalizedEmail = normalizedEmail;
        admin.IsActive = request.AdminIsActive;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlatformDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalTenants = await repository.CountAllTenantsAsync(cancellationToken);

        var activeTenants = await repository.CountTenantsByStatusAsync(TenantStatus.Active, cancellationToken);

        var suspendedTenants = await repository.CountTenantsByStatusAsync(TenantStatus.Suspended, cancellationToken);

        var failedTenants = await repository.CountTenantsByStatusAsync(TenantStatus.Failed, cancellationToken);

        var provisioningTenants = await repository.CountTenantsByStatusAsync(TenantStatus.Provisioning, cancellationToken);

        var totalUsers = await repository.CountAllUsersAsync(cancellationToken);

        var tenantAdmins = await repository.CountUsersByRoleAsync(UserRole.TenantAdmin, cancellationToken);

        var regularUsers = await repository.CountUsersByRoleAsync(UserRole.RegularUser, cancellationToken);

        return new PlatformDashboardDto
        {
            TotalTenants = totalTenants,
            ActiveTenants = activeTenants,
            SuspendedTenants = suspendedTenants,
            FailedTenants = failedTenants,
            ProvisioningTenants = provisioningTenants,
            TotalUsers = totalUsers,
            TenantAdmins = tenantAdmins,
            RegularUsers = regularUsers
        };
    }
}
