
using FactorEdge.Application.Interfaces.Repositories;
using FactorEdge.Application.Interfaces.Tenants;
using FactorEdge.Domain.Enums;
using System.Security.Claims;

namespace FactorEdge.Web.Middlewares;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IPlatformRepository platformRepository, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (context.User.IsInRole(nameof(UserRole.SuperAdmin)))
        {
            await next(context);
            return;
        }

        if (!context.User.IsInRole(nameof(UserRole.TenantAdmin)) &&
            !context.User.IsInRole(nameof(UserRole.RegularUser)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var tenantIdValue = context.User.FindFirstValue("tenantId");

        if (!Guid.TryParse(tenantIdValue, out var tenantId) || tenantId == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var databaseName = await platformRepository.GetActiveTenantDatabaseNameAsync(tenantId, context.RequestAborted);

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        tenantContext.SetTenant(databaseName);

        await next(context);
    }
}
