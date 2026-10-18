
using System.Security.Claims;
using FactorEdge.Application.DTOs.Cameras;
using FactorEdge.Application.Interfaces.Services.Camera;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactorEdge.Web.Controllers;

[Authorize]
[Route("Camera")]
public sealed class CameraController(ICameraService cameraService, ICameraStatusHistoryService historyService, ILogger<CameraController> logger) : Controller
{
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out _))
            return Forbid();

        var cameras = await cameraService.GetAllAsync(cancellationToken);

        ViewBag.CanManage = CanManageCameras();

        return View(cameras);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        if (!CanManageCameras() || !TryGetTenantId(out _))
            return Forbid();

        return View(new CreateCameraRequest());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCameraRequest request, CancellationToken cancellationToken)
    {
        if (!CanManageCameras() || !TryGetTenantId(out var tenantId))
            return Forbid();

        if (!ModelState.IsValid)
            return View(request);

        try
        {
            await cameraService.CreateAsync(tenantId, request, cancellationToken);

            TempData["Success"] = "Kamera oluşturuldu. Docker container başlatıldı.";

            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            return View(request);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            return View(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,"Kamera oluşturulamadı.");

            ModelState.AddModelError(string.Empty, "Kamera oluşturulurken beklenmeyen bir hata oluştu.");

            return View(request);
        }
    }

    [HttpGet("Edit/{deviceId:guid}")]
    public async Task<IActionResult> Edit(Guid deviceId, CancellationToken cancellationToken)
    {
        if (!CanManageCameras() || !TryGetTenantId(out _))
            return Forbid();

        var camera = await cameraService.GetByIdAsync(deviceId, cancellationToken);

        if (camera is null)
            return NotFound();

        return View(camera);
    }

    [HttpPost("Edit/{deviceId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid deviceId, UpdateCameraRequest request, CancellationToken cancellationToken)
    {
        if (!CanManageCameras() || !TryGetTenantId(out var tenantId))
            return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Kamera bilgileri geçersiz.";

            return RedirectToAction(nameof(Edit), new { deviceId });
        }

        try
        {
            var camera = await cameraService.UpdateAsync(tenantId, deviceId, request, cancellationToken);

            if (camera is null)
                return NotFound();

            TempData["Success"] = "Kamera bilgileri güncellendi.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kamera güncellenemedi. DeviceId: {DeviceId}", deviceId);

            TempData["Error"] = 
                ex is ArgumentException or InvalidOperationException
                    ? ex.Message
                    : "Kamera güncellenirken beklenmeyen bir hata oluştu.";

            return RedirectToAction(nameof(Edit), new { deviceId });
        }
    }

    [HttpPost("Delete/{deviceId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid deviceId, CancellationToken cancellationToken)
    {
        if (!CanManageCameras() || !TryGetTenantId(out var tenantId))
            return Forbid();

        try
        {
            var deleted = await cameraService.DeleteAsync(tenantId, deviceId, cancellationToken);

            if (!deleted)
                return NotFound();

            TempData["Success"] = "Kamera ve Docker container silindi.";
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Kamera silinemedi. DeviceId: {DeviceId}",
                deviceId);

            TempData["Error"] = "Kamera silinirken hata oluştu.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("History")]
    public async Task<IActionResult> History(CancellationToken cancellationToken) 
    {
        if (!TryGetTenantId(out var tenantId))
            return Forbid();

        var histories = await historyService.GetRecentAsync(tenantId, 50, cancellationToken);

        return Json(histories);
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        var value =
            User.FindFirst("tenantId")?.Value ??
            User.FindFirst("TenantId")?.Value;

        return Guid.TryParse(value, out tenantId) && tenantId != Guid.Empty;
    }

    private bool CanManageCameras()
    {
        // Projede TenantAdmin rolü isim veya enum sayısı
        // olarak claim'e yazılmış olabilir.
        return User.IsInRole("TenantAdmin") || User.IsInRole("2");
    }
}
