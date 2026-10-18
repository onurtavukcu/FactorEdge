
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FactorEdge.Infrastructure.Persistence.Repositories;
public sealed class PlatformRepository(BaseDbContext dbContext) : IPlatformRepository
{
    public async Task AddTenantAsync(Tenant tenant, CancellationToken ct = default)
    {
        await dbContext.Tenants.AddAsync(tenant, ct);
    }

    public Task<List<Tenant>> GetTenantsAsync(bool onlyActive, CancellationToken ct = default)
    {
        var query = dbContext.Tenants.AsNoTracking();

        if (onlyActive)
            query = query.Where(x => x.Status == TenantStatus.Active);

        return query.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Tracking açık: UpdateAsync entity'yi değiştirecek.
        return dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct);
    }

    public Task<ApplicationUser?> GetTenantAdminAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Tracking açık.
        return dbContext.Users.SingleOrDefaultAsync(
            x => x.TenantId == tenantId &&
                 x.Role == UserRole.TenantAdmin,
            ct);
    }

    public Task<List<ApplicationUser>> GetRegularUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        return dbContext.Users
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Role == UserRole.RegularUser)
            .OrderBy(x => x.UserName)
            .ToListAsync(ct);
    }

    public Task<ApplicationUser?> GetRegularUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        // Tracking açık: güncellemede kullanılacak.
        return dbContext.Users.FirstOrDefaultAsync(
            x => x.TenantId == tenantId &&
                 x.Id == userId &&
                 x.Role == UserRole.RegularUser,
            ct);
    }

    public void AddUser(ApplicationUser user)
    {
        dbContext.Users.Add(user);
    }

    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludeUserId = null, CancellationToken ct = default) 
    {
        return dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.NormalizedEmail == normalizedEmail &&
                     (!excludeUserId.HasValue ||
                      x.Id != excludeUserId.Value),
                ct);
    }

    public Task<bool> IsTenantActiveAsync(Guid tenantId, CancellationToken ct = default)
    {
        return dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == tenantId &&
                     x.Status == TenantStatus.Active,
                ct);
    }

    public Task<int> CountUsersAsync(Guid tenantId, bool? isActive = null, CancellationToken ct = default)
    {
        return dbContext.Users
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId &&
                     (!isActive.HasValue ||
                      x.IsActive == isActive.Value),
                ct);
    }

    public Task<int> CountAllTenantsAsync(CancellationToken ct = default)
    {
        return dbContext.Tenants.CountAsync(ct);
    }

    public Task<int> CountTenantsByStatusAsync(TenantStatus status,         CancellationToken ct = default)
    {
        return dbContext.Tenants.CountAsync(x => x.Status == status, ct);
    }

    public Task<int> CountAllUsersAsync(CancellationToken ct = default) 
    {
        return dbContext.Users.CountAsync(ct);
    }

    public Task<int> CountUsersByRoleAsync(UserRole role, CancellationToken ct = default)
    {
        return dbContext.Users.CountAsync(
            x => x.Role == role, ct);
    }

    public Task<string?> GetActiveTenantDatabaseNameAsync(Guid tenantId, CancellationToken ct = default)
    {
        return dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId &&
                        x.Status == TenantStatus.Active)
            .Select(x => x.DatabaseName)
            .SingleOrDefaultAsync(ct);
    }

    public Task<List<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) 
    {
        return dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Status == TenantStatus.Active)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Users_NormalizedEmail"
            })
        {
            throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.", ex);
        }
    }
}
