using HRM.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRM.Web.Controllers
{
    public class PositionController : BaseController
    {
        public PositionController(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public async Task<IActionResult> Index()
        {
            var client = GetClient();
            var response = await client.GetAsync("/api/organization/positions");
            if (response.IsSuccessStatusCode)
            {
                var positions = await response.Content.ReadFromJsonAsync<List<PositionViewModel>>();
                return View("List", positions);
            }
            return View("List", new List<PositionViewModel>());
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDepartmentsDropDownList();
            return View("Modify", new PositionViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(PositionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDepartmentsDropDownList();
                return View("Modify", model);
            }

            var client = GetClient();
            await client.PostAsJsonAsync("/api/organization/positions", model);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var client = GetClient();
            var position = await client.GetFromJsonAsync<PositionViewModel>($"/api/organization/positions/{id}");
            if (position == null) return NotFound();

            await PopulateDepartmentsDropDownList(position.DepartmentId);
            return View("Modify", position);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Guid id, PositionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDepartmentsDropDownList(model.DepartmentId);
                return View("Modify", model);
            }

            var client = GetClient();
            await client.PutAsJsonAsync($"/api/organization/positions/{id}", model);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = GetClient();
            await client.DeleteAsync($"/api/organization/positions/{id}");
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDepartmentsDropDownList(object? selectedDepartment = null)
        {
            var client = GetClient();
            var departments = await client.GetFromJsonAsync<List<DepartmentViewModel>>("/api/organization/departments");
            ViewBag.DepartmentId = new SelectList(departments, "DepartmentId", "Name", selectedDepartment);
        }
    }
}
