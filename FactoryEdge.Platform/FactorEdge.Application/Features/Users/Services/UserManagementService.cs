
using System.ComponentModel.DataAnnotations;
using FactorEdge.Application.Features.Users.Models;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Users;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace FactorEdge.Application.Features.Users.Services;

public sealed class UserManagementService(IPlatformRepository repository, ITenantDashboardRepository dashboardRepository, IPasswordHasher<ApplicationUser> passwordHasher) : IUserManagementService
{
    public async Task<IReadOnlyList<RegularUserDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default) 
    {
        var users = await repository.GetRegularUsersAsync(tenantId, cancellationToken);

        return users.Select(ToDto).ToList();
    }

    public async Task<RegularUserDto?> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetRegularUserAsync(tenantId, userId, cancellationToken);

        return user is null ? null : ToDto(user);
    }

    public async Task CreateAsync(Guid tenantId, CreateRegularUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveTenantAsync(tenantId, cancellationToken);

        var userName = request.UserName?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";

        ValidateUserInformation(userName, email);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ArgumentException("Şifre en az 8 karakter olmalıdır.");
        }

        var normalizedEmail = email.ToUpperInvariant();

        if (await repository.EmailExistsAsync(normalizedEmail, ct: cancellationToken))
        {
            throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = userName,
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = UserRole.RegularUser,
            IsActive = true
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        repository.AddUser(user);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Guid tenantId, UpdateRegularUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureActiveTenantAsync(tenantId, cancellationToken);

        var user = await repository.GetRegularUserAsync(tenantId, request.UserId, cancellationToken);

        if (user is null)
            throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        var userName = request.UserName?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";

        ValidateUserInformation(userName, email);

        var normalizedEmail = email.ToUpperInvariant();

        if (await repository.EmailExistsAsync(normalizedEmail, user.Id, cancellationToken))
        {
            throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");
        }

        user.UserName = userName;
        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.IsActive = request.IsActive;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<TenantDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureActiveTenantAsync(tenantId, cancellationToken);

        var totalDevices = await dashboardRepository.CountDevicesAsync(cancellationToken);

        var totalTelemetry = await dashboardRepository.CountTelemetryAsync(cancellationToken);

        var totalEvents = await dashboardRepository.CountEventLogsAsync(cancellationToken);

        var totalUsers = await repository.CountUsersAsync(tenantId, ct: cancellationToken); 

        var activeUsers = await repository.CountUsersAsync(tenantId, isActive: true, ct: cancellationToken);

        var efficiency = await dashboardRepository.GetEfficiencyLastHourAsync(cancellationToken);

        return new TenantDashboardDto
        {
            TotalDevices = totalDevices,
            TotalTelemetryRecords = totalTelemetry,
            TotalEventLogs = totalEvents,
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            Efficiency = efficiency
        };
    }

    private async Task EnsureActiveTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant bilgisi bulunamadı.");

        var isActive = await repository.IsTenantActiveAsync(tenantId, cancellationToken);

        if (!isActive)
            throw new UnauthorizedAccessException("Tenant aktif değil.");
    }

    private static void ValidateUserInformation(string userName, string email)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 100)
        {
            throw new ArgumentException("Geçerli bir kullanıcı adı giriniz.");
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 ||
            !new EmailAddressAttribute().IsValid(email))
        {
            throw new ArgumentException("Geçerli bir e-posta adresi giriniz.");
        }
    }

    private static RegularUserDto ToDto(ApplicationUser user)
    {
        return new RegularUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }
}
