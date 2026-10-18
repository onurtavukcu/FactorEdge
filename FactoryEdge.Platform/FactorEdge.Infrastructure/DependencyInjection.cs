using FactorEdge.Application.Features.Auth.Models;
using FactorEdge.Application.Features.Auth.Services;
using FactorEdge.Application.Features.Camera.Service;
using FactorEdge.Application.Features.Tenants.Services;
using FactorEdge.Application.Features.Users.Services;
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Services.Auth;
using FactorEdge.Application.Interfaces.Services.Camera;
using FactorEdge.Application.Interfaces.Services.Simulator;
using FactorEdge.Application.Interfaces.Services.Tenants;
using FactorEdge.Application.Interfaces.Services.Users;
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Domain.Entities;
using FactorEdge.Infrastructure.DbContexts;
using FactorEdge.Infrastructure.Options;
using FactorEdge.Infrastructure.Persistence.DbContextFactory;
using FactorEdge.Infrastructure.Persistence.Repositories;
using FactorEdge.Infrastructure.Persistence.Seeder;
using FactorEdge.Infrastructure.Seeder;
using FactorEdge.Infrastructure.Services.Camera;
using FactorEdge.Infrastructure.Services.Simulator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactorEdge.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var baseConnectionString = configuration.GetConnectionString("FactorEdgeBaseDatabase")?? throw new InvalidOperationException("FactorEdgeBaseDatabase connection string not found.");
         
            services.AddDbContext<BaseDbContext>(options =>options.UseNpgsql(baseConnectionString));
            services.Configure<SimulatorProvisioningOptions>(configuration.GetSection("SimulatorProvisioning"));

            services.Configure<SuperAdminSettings>(configuration.GetSection(SuperAdminSettings.SectionName));            
            services.AddScoped<IPasswordHasher<ApplicationUser>,PasswordHasher<ApplicationUser>>();
            services.AddScoped<SuperAdminSeeder>();
            services.AddScoped<TenantAdminSeeder>();
            services.AddSingleton<TenantDbContextFactory>();
            services.AddScoped<ITenantDatabaseProvisioner, TenantDatabaseProvisioner>();
            services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
            services.AddScoped<ITenantContext, TenantContext>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITenantManagementService, TenantManagementService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<ISimulatorProvisioningService, SimulatorProvisioningService>();
            services.AddScoped<ICameraRepository, CameraRepository>();
            services.AddScoped<ICameraProvisioningService, CameraProvisioningService>();
            services.AddScoped<ICameraStatusHistoryService, CameraStatusHistoryService>();
            services.AddHttpContextAccessor();

            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

            services.AddScoped<TenantDbContext>(sp =>
            {
                var tenantContext = sp.GetRequiredService<ITenantContext>();

                var databaseName = tenantContext.DatabaseName
                    ?? throw new InvalidOperationException(
                        "Tenant context has not been initialized.");

                var factory = sp.GetRequiredService<TenantDbContextFactory>();

                return factory.Create(databaseName);
            });

            services.AddScoped<IDeviceTelemetryRepository, DeviceTelemetryRepository>();
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<IDeviceEventLogRepository, DeviceEventLogRepository>();
            services.AddScoped<IAuthRepository, AuthRepository>();
            services.AddScoped<IPlatformRepository, PlatformRepository>();
            services.AddScoped<ITenantProvisioningRepository, TenantProvisioningRepository>();
            services.AddScoped<ITenantDashboardRepository, TenantDashboardRepository>();

            return services;
        }
    }
}