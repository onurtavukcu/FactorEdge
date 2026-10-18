using FactorEdge.Application.DTOs.Devices;
using FactorEdge.Application.Interfaces.Services.Device;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactorEdge.Web.Controllers
{
    [Authorize(Roles = "TenantAdmin,RegularUser")]
    public sealed class DevicesController(IDeviceService deviceService, IDeviceTelemetryService deviceTelemetryService, IDeviceEventLogService eventLogService) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var devices = await deviceService.GetAllAsync(cancellationToken);

            return View(devices);
        }

        [HttpGet]
        [Authorize(Roles = "TenantAdmin")]
        public IActionResult Create()
        {
            return View(new CreateDeviceRequest());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Create(CreateDeviceRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View(request);

            var tenantIdValue = User.FindFirst("tenantId")?.Value;

            if (!Guid.TryParse(tenantIdValue, out var tenantId) ||tenantId == Guid.Empty)
            {
                return Forbid();
            }

            try
            {
                await deviceService.CreateAsync(tenantId, request, cancellationToken);

                TempData["Success"] = "Cihaz oluşturuldu ve Docker simülatörü başlatıldı.";

                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Simülatör oluşturulamadı. Docker bağlantısını ve uygulama loglarını kontrol edin.");
            }

            return View(request);
        }


        [HttpGet]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
        {
            var device = await deviceService.GetForEditAsync(id, cancellationToken);

            if (device is null)
                return NotFound();

            return View(device);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Edit(UpdateDeviceRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View(request);

            try
            {
                await deviceService.UpdateAsync(request, cancellationToken);

                TempData["Success"] = "Cihaz güncellendi.";

                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(request);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var tenantIdValue = User.FindFirst("tenantId")?.Value;

            if (!Guid.TryParse(tenantIdValue, out var tenantId) || tenantId == Guid.Empty)
            {
                return Forbid();
            }

            try
            {
                await deviceService.SoftDeleteAsync(tenantId, id, cancellationToken);

                TempData["Success"] = "Cihaz ve Docker simülatörü silindi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Cihaz silinemedi. Docker bağlantısını ve servis loglarını kontrol edin.";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken) 
        {
            var devices = await deviceService.GetAllAsync(cancellationToken);
            var device = devices.FirstOrDefault(x => x.Id == id);

            if (device is null)
                return NotFound();

            var telemetries = await deviceTelemetryService.GetRecentAsync(id, 50, cancellationToken);

            var events = await eventLogService.GetRecentAsync(id, 30, cancellationToken);

            return View(new DeviceTelemetryDetailsDto
            {
                Device = device,
                Telemetries = telemetries,
                Events = events
            });
        }

        [HttpGet]
        public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken) 
        {
            try
            {
                var history = await deviceTelemetryService.GetHistoryAsync(id, cancellationToken);

                return Json(history);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}