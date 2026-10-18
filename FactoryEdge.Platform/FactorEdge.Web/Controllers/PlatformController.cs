
using FactorEdge.Application.Features.Tenants.Models;
using FactorEdge.Application.Interfaces.Services.Tenants;
using FactorEdge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactorEdge.Web.Controllers;

[Authorize(Roles = nameof(UserRole.SuperAdmin))]
public sealed class PlatformController(ITenantManagementService tenantManagementService, ITenantProvisioningService tenantProvisioningService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool isActive = true, CancellationToken cancellationToken = default)
    {
        var tenants = await tenantManagementService.GetAllAsync(isActive, cancellationToken);

        ViewBag.IsActive = isActive;

        return View(tenants);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await tenantManagementService.GetByIdAsync(id, cancellationToken);

        if (tenant is null)
            return NotFound();

        return View(tenant);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateTenantRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(request);

        try
        {
            await tenantProvisioningService.CreateAsync(request, cancellationToken);

            TempData["Success"] = "Tenant başarıyla oluşturuldu.";

            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "Tenant oluşturulamadı. Bilgileri kontrol edin.");

            return View(request);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var model = await tenantManagementService.GetForEditAsync(id, cancellationToken);

            if (model is null)
                return NotFound();

            return View(model);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;

            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TenantEditDto model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        var request = new UpdateTenantRequest
        {
            TenantId = id,
            Name = model.Name,
            Status = model.Status,
            AdminUserName = model.AdminUserName,
            AdminEmail = model.AdminEmail,
            AdminIsActive = model.AdminIsActive
        };

        try
        {
            await tenantManagementService.UpdateAsync(request, cancellationToken);

            TempData["Success"] = "Tenant bilgileri güncellendi.";

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var model = await tenantManagementService.GetDashboardAsync(cancellationToken);

        return View(model);
    }
}
