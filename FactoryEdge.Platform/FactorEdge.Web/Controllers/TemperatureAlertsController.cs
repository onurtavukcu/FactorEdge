using FactorEdge.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FactorEdge.Web.Controllers
{

    [Authorize(Roles = "TenantAdmin,RegularUser")]
    [Route("temperature-alerts")]
    public sealed class TemperatureAlertsController(IDeviceRepository deviceRepository) : Controller
    {
        [HttpGet("active")]
        public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
        {
            var tenantIdClaim = User.FindFirstValue("tenantId");

            if (!Guid.TryParse(tenantIdClaim, out var tenantId) ||
                tenantId == Guid.Empty)
            {
                return Forbid();
            }

            // Tenant middleware mevcut kullanıcının tenant DB'sini
            // seçmiş olmalı. Repository bu tenant DB'den okur.
            var devices = await deviceRepository.GetAllAsync(cancellationToken);

            var alerts = devices
                .Where(x => !x.IsDeleted &&
                            x.IsActive &&
                            x.IsTemperatureAlarmActive)
                .Select(x => new
                {
                    deviceId = x.Id,
                    deviceCode = x.DeviceCode,
                    deviceName = x.Name,
                    temperature = x.TemperatureAlarmCelsius,
                    threshold = 85.0,
                    changedAtUtc = x.TemperatureAlarmChangedAtUtc
                })
                .ToList();

            return Ok(alerts);
        }
    }
}
