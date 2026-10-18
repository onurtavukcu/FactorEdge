
using FactorEdge.Application;
using FactorEdge.Application.Features.Auth.Models;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Domain.Enums;
using FactorEdge.Infrastructure;
using FactorEdge.Infrastructure.Persistence;
using FactorEdge.Web.Background_Services;
using FactorEdge.Web.Hubs;
using FactorEdge.Web.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DeviceConnectionMonitor>();
builder.Services.AddHostedService<DeviceTelemetryBroadcastService>();
// Authentication

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not found.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
            string.IsNullOrWhiteSpace(jwtSettings.Audience) ||
            string.IsNullOrWhiteSpace(jwtSettings.Key))
        {
            throw new InvalidOperationException("JWT configuration is incomplete.");
        }

        byte[] keyBytes;

        try
        {
            keyBytes = Convert.FromBase64String(jwtSettings.Key);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "JWT signing key must be Base64 encoded.", ex);
        }

        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Invalid JWT signing key.");

        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,

            ClockSkew = TimeSpan.FromSeconds(30),

            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // API / Postman: Authorization: Bearer <token>
                var authorization =
                    context.Request.Headers.Authorization.ToString();

                if (authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                // MVC: JWT, HttpOnly Cookie içerisinde taşınır.
                if (context.Request.Cookies.TryGetValue(
                    "FactorEdge.AccessToken",
                    out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },


            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                if (!Guid.TryParse(userIdValue, out var userId) ||
                    userId == Guid.Empty)
                {
                    context.Fail("Invalid user.");
                    return;
                }

                var authRepository = context.HttpContext.RequestServices
                    .GetRequiredService<IAuthRepository>();

                var user = await authRepository.GetUserByIdAsync(
                    userId,
                    context.HttpContext.RequestAborted);

                if (user is null || !user.IsActive)
                {
                    context.Fail("User is inactive or missing.");
                    return;
                }

                var tokenRole = context.Principal?
                    .FindFirstValue(ClaimTypes.Role);

                var tokenTenantId = context.Principal?
                    .FindFirstValue("tenantId");

                if (tokenRole != user.Role.ToString() ||
                    tokenTenantId != user.TenantId?.ToString())
                {
                    context.Fail("User authorization data has changed.");
                    return;
                }

                if (user.Role == UserRole.SuperAdmin)
                {
                    if (user.TenantId is not null)
                    {
                        context.Fail("Invalid SuperAdmin tenant.");
                    }

                    return;
                }

                if (user.Role is not (
                    UserRole.TenantAdmin or UserRole.RegularUser) ||
                    !user.TenantId.HasValue)
                {
                    context.Fail("Invalid tenant user.");
                    return;
                }

                var tenantActive = await authRepository.IsTenantActiveAsync(
                    user.TenantId.Value,
                    context.HttpContext.RequestAborted);

                if (!tenantActive)
                {
                    context.Fail("Tenant is inactive.");
                    return;
                }

                if (user.Role == UserRole.RegularUser)
                {
                    var tenantAdminIsActive = await authRepository.IsTenantAdminActiveAsync(
                        user.TenantId.Value,
                        context.HttpContext.RequestAborted);

                    if (!tenantAdminIsActive)
                    {
                        context.Fail("Tenant admin is inactive.");
                        return;
                    }
                }
            },


            OnChallenge = context =>
            {
                // MVC sayfalarında login ekranına yönlendir.
                // API isteklerinde standart 401 cevabını koru.
                if (!context.Response.HasStarted &&
                    !context.Request.Path.StartsWithSegments("/api") &&
                    !context.Request.Headers.Accept.ToString()
                        .Contains("application/json",
                            StringComparison.OrdinalIgnoreCase))
                {
                    context.HandleResponse();

                    var returnUrl =
                        context.Request.PathBase +
                        context.Request.Path +
                        context.Request.QueryString;

                    context.Response.Redirect(
                        "/account/login?returnUrl=" +
                        Uri.EscapeDataString(returnUrl.ToString()));
                }

                return Task.CompletedTask;
            },

            OnForbidden = context =>
            {
                // Yetkisiz rol: MVC için AccessDenied ekranı.
                if (!context.Response.HasStarted &&
                    !context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.Redirect("/account/access-denied");
                }

                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddAuthorization();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Sıralama önemli.
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers();
app.MapHub<DeviceTelemetryHub>("/hubs/device-telemetry");

app.MapGet("/", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Redirect("/account/login");

    if (context.User.IsInRole(nameof(UserRole.SuperAdmin)))
        return Results.Redirect("/Platform/Dashboard");

    if (context.User.IsInRole(nameof(UserRole.TenantAdmin)) ||
        context.User.IsInRole(nameof(UserRole.RegularUser)))
        return Results.Redirect("/Home/Index");

    return Results.Redirect("/account/access-denied");
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
