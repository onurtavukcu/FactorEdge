
using FactorEdge.Application.Features.Auth.Models;
using FactorEdge.Application.Interfaces.Services.Auth;
using FactorEdge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactorEdge.Web.Controllers;

[Route("account")]
public sealed class AccountController(IAuthService authService, IJwtTokenService jwtTokenService, IOptions<JwtSettings> jwtOptions) : Controller
{
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.IsInRole(nameof(UserRole.SuperAdmin))
                ? RedirectToAction("Dashboard", "Platform")
                : RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(request);

        var result = await authService.LoginAsync(request, cancellationToken);

        if (!result.IsSuccess || result.User is null)
        {
            var message = result.Status switch
            {
                LoginStatus.UserInactive => "Kullanıcı hesabınız pasif durumda.",

                LoginStatus.TenantInactive => "Bağlı olduğunuz şirketin hesabı aktif değil.",

                _ => "Email veya şifre hatalı."
            };

            ModelState.AddModelError("", message);
            return View(request);
        }

        // Başarılı login → JWT oluştur.
        var token = jwtTokenService.GenerateToken(result.User);

        // JWT'yi tarayıcıya HttpOnly cookie ile gönder.
        Response.Cookies.Append(
            "FactorEdge.AccessToken",
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(
                    jwtOptions.Value.ExpirationMinutes)
            });

        return result.User.Role == UserRole.SuperAdmin
            ? RedirectToAction("Dashboard", "Platform")
            : RedirectToAction("Index", "Home");
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await authService.LogoutAsync();

        return RedirectToAction(nameof(Login));
    }

    [HttpGet("access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
