using HRM.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRM.Web.Controllers
{
    public class DepartmentController : BaseController
    {
        public DepartmentController(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public async Task<IActionResult> Index()
        {
            var client = GetClient();
            var response = await client.GetAsync("/api/organization/departments");
            if (response.IsSuccessStatusCode)
            {
                var departments = await response.Content.ReadFromJsonAsync<List<DepartmentViewModel>>();
                return View("List", departments);
            }
            return View("List", new List<DepartmentViewModel>());
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartmentsByCompany(Guid companyId)
        {
            var client = GetClient();
            var response = await client.GetAsync($"/api/organization/companies/{companyId}/departments");
            if (!response.IsSuccessStatusCode)
            {
                return Json(new List<SelectListItem>());
            }
            var departments = await response.Content.ReadFromJsonAsync<List<DepartmentViewModel>>();
            var selectList = departments?.Select(d => new SelectListItem { Value = d.DepartmentId.ToString(), Text = d.Name }).ToList();
            return Json(selectList);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateCompaniesDropDownList();
            return View("Modify", new DepartmentViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(DepartmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCompaniesDropDownList();
                return View("Modify", model);
            }

            var client = GetClient();
            await client.PostAsJsonAsync("/api/organization/departments", model);
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateParentDepartmentsDropDownList(Guid companyId, Guid? excludeId = null, object? selectedValue = null)
        {
            var client = GetClient();
            var allDepartments = await client.GetFromJsonAsync<List<DepartmentViewModel>>($"/api/organization/companies/{companyId}/departments");
            var selectableDepartments = allDepartments?
                .Where(d => d.DepartmentId != excludeId).ToList()
                                      ?? new List<DepartmentViewModel>();
            ViewBag.ParentId = new SelectList(selectableDepartments, "DepartmentId", "Name", selectedValue);
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var client = GetClient();
            var department = await client.GetFromJsonAsync<DepartmentViewModel>($"/api/organization/departments/{id}");
            if (department == null) return NotFound();

            await PopulateCompaniesDropDownList(department.CompanyId);
            await PopulateParentDepartmentsDropDownList(department.CompanyId, id);
            return View("Modify", department);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Guid id, DepartmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateCompaniesDropDownList(model.CompanyId);
                return View("Modify", model);
            }

            var client = GetClient();
            await client.PutAsJsonAsync($"/api/organization/departments/{id}", model);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = GetClient();
            await client.DeleteAsync($"/api/organization/departments/{id}");
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCompaniesDropDownList(object? selectedCompany = null)
        {
            var client = GetClient();
            var companies = await client.GetFromJsonAsync<List<CompanyViewModel>>("/api/organization/companies");
            ViewBag.CompanyId = new SelectList(companies, "CompanyId", "Name", selectedCompany);
        }
    }
}
