
using FactorEdge.Application.Features.Auth.Models;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Auth;
using FactorEdge.Domain.Entities;
using FactorEdge.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace FactorEdge.Application.Features.Auth.Services;

public sealed class AuthService(
    IAuthRepository authRepository,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IHttpContextAccessor httpContextAccessor) : IAuthService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new LoginResult
            {
                Status = LoginStatus.InvalidCredentials
            };
        }

        var normalizedEmail = request.Email
            .Trim()
            .ToUpperInvariant();

        var user = await authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return new LoginResult
            {
                Status = LoginStatus.InvalidCredentials
            };
        }

        var passwordResult = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return new LoginResult
            {
                Status = LoginStatus.InvalidCredentials
            };
        }

        if (!user.IsActive)
        {
            return new LoginResult
            {
                Status = LoginStatus.UserInactive
            };
        }

        if (user.Role == UserRole.SuperAdmin)
        {
            if (user.TenantId is not null)
            {
                return new LoginResult
                {
                    Status = LoginStatus.InvalidCredentials
                };
            }
        }
        else if (user.Role is UserRole.TenantAdmin or UserRole.RegularUser)
        {
            if (user.TenantId is null)
            {
                return new LoginResult
                {
                    Status = LoginStatus.InvalidCredentials
                };
            }

            var tenantIsActive = await authRepository.IsTenantActiveAsync(
                user.TenantId.Value,
                cancellationToken);

            if (!tenantIsActive)
            {
                return new LoginResult
                {
                    Status = LoginStatus.TenantInactive
                };
            }

            if (user.Role == UserRole.RegularUser)
            {
                var tenantAdminIsActive = await authRepository.IsTenantAdminActiveAsync(
                    user.TenantId.Value,
                    cancellationToken);

                if (!tenantAdminIsActive)
                {
                    return new LoginResult
                    {
                        Status = LoginStatus.TenantInactive
                    };
                }
            }
        }
        else
        {
            return new LoginResult
            {
                Status = LoginStatus.InvalidCredentials
            };
        }

        return new LoginResult
        {
            Status = LoginStatus.Success,
            User = new LoginResponse
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                UserName = user.UserName,
                Email = user.Email,
                Role = user.Role
            }
        };
    }

    public Task LogoutAsync()
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "HttpContext is not available.");

        context.Response.Cookies.Delete(
            "FactorEdge.AccessToken",
            new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });

        return Task.CompletedTask;
    }
}
