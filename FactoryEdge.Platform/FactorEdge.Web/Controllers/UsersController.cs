using FactorEdge.Application.Features.Users.Models;
using FactorEdge.Application.Interfaces.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FactorEdge.Web.Controllers
{
    [Authorize(Roles = "TenantAdmin")]
    public sealed class UsersController(IUserManagementService userManagementService) : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var tenantId = GetTenantId();

            if (tenantId is null)
                return Forbid();

            var users = await userManagementService.GetAllAsync(tenantId.Value, cancellationToken);

            return View(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateRegularUserRequest());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateRegularUserRequest model, CancellationToken cancellationToken)
        {
            var tenantId = GetTenantId();

            if (tenantId is null)
                return Forbid();

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                await userManagementService.CreateAsync(tenantId.Value, model, cancellationToken);

                TempData["Success"] = "Kullanıcı oluşturuldu.";

                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
        {
            var tenantId = GetTenantId();

            if (tenantId is null)
                return Forbid();

            var user = await userManagementService.GetByIdAsync(tenantId.Value, id, cancellationToken);

            if (user is null)
                return NotFound();

            var model = new UpdateRegularUserRequest
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                IsActive = user.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UpdateRegularUserRequest model, CancellationToken cancellationToken)
        {
            var tenantId = GetTenantId();

            if (tenantId is null)
                return Forbid();

            if (id != model.UserId)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                await userManagementService.UpdateAsync(tenantId.Value, model, cancellationToken);

                TempData["Success"] = "Kullanıcı güncellendi.";

                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }

            return View(model);
        }

        private Guid? GetTenantId()
        {
            var tenantIdClaim = User.FindFirstValue("tenantId");

            if (!Guid.TryParse(tenantIdClaim, out var tenantId) ||
                tenantId == Guid.Empty)
            {
                return null;
            }

            return tenantId;
        }
    }
}
