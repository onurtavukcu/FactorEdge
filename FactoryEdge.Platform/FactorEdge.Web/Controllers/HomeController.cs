using FactorEdge.Application.Interfaces.Services.Users;
using FactorEdge.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace FactorEdge.Web.Controllers
{
    [Authorize(Roles = "TenantAdmin,RegularUser")]
    public class HomeController(IUserManagementService userManagementService) : Controller
    {

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var tenantIdClaim = User.FindFirstValue("tenantId");

            if (!Guid.TryParse(tenantIdClaim, out var tenantId) ||
                tenantId == Guid.Empty)
            {
                return Forbid();
            }

            var model = await userManagementService.GetDashboardAsync(tenantId, cancellationToken);

            return View(model);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
