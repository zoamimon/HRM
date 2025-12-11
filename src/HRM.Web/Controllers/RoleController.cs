using Microsoft.AspNetCore.Mvc;

namespace HRM.Web.Controllers
{
    public class RoleController : BaseController
    {
        public RoleController(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public async Task<IActionResult> Index()
        {
            var client = GetClient();
            var roles = await client.GetFromJsonAsync<List<HRM.Web.ViewModels.RoleViewModel>>("/api/identity/roles");
            return View("List", roles);
        }

        public async Task<IActionResult> Modify(int? id)
        {
            var client = GetClient();
            var allPermissions = await client.GetFromJsonAsync<List<HRM.Web.ViewModels.AssignedPermissionViewModel>>("/api/identity/roles/permissions");

            var viewModel = new HRM.Web.ViewModels.RoleModificationViewModel();

            if (id.HasValue) // Edit mode
            {
                var roleDetails = await client.GetFromJsonAsync<RoleDetailsApiResponse>($"/api/identity/roles/{id.Value}");
                if (roleDetails == null) return NotFound();

                viewModel.RoleId = roleDetails.RoleId;
                viewModel.Name = roleDetails.Name;

                var assignedPermissionIds = new HashSet<int>(roleDetails.PermissionIds);
                viewModel.Permissions = allPermissions.Select(p => new HRM.Web.ViewModels.AssignedPermissionViewModel
                {
                    PermissionId = p.PermissionId,
                    Name = p.Name,
                    IsAssigned = assignedPermissionIds.Contains(p.PermissionId)
                }).ToList();
            }
            else // Create mode
            {
                viewModel.Permissions = allPermissions.Select(p => new HRM.Web.ViewModels.AssignedPermissionViewModel
                {
                    PermissionId = p.PermissionId,
                    Name = p.Name,
                    IsAssigned = false
                }).ToList();
            }

            return View("Modify", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Modify(HRM.Web.ViewModels.RoleModificationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Modify", model);
            }

            var client = GetClient();
            var command = new
            {
                Name = model.Name,
                PermissionIds = model.Permissions.Where(p => p.IsAssigned).Select(p => p.PermissionId)
            };

            if (model.RoleId > 0) // Edit
            {
                await client.PutAsJsonAsync($"/api/identity/roles/{model.RoleId}", command);
            }
            else // Create
            {
                await client.PostAsJsonAsync("/api/identity/roles", command);
            }

            return RedirectToAction(nameof(Index));
        }

        // Helper class to match API response
        private class RoleDetailsApiResponse
        {
            public int RoleId { get; set; }
            public string Name { get; set; }
            public IEnumerable<int> PermissionIds { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var client = GetClient();
            await client.DeleteAsync($"/api/identity/roles/{id}");
            return RedirectToAction(nameof(Index));
        }
    }
}
