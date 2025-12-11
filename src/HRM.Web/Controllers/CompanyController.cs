using HRM.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRM.Web.Controllers
{
    public class CompanyController : BaseController
    {
        public CompanyController(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public async Task<IActionResult> Index()
        {
            var client = GetClient();
            var response = await client.GetAsync("/api/organization/companies");
            if (response.IsSuccessStatusCode)
            {
                var companies = await response.Content.ReadFromJsonAsync<List<CompanyViewModel>>();
                return View("List", companies);
            }

            ModelState.AddModelError(string.Empty, "An error occurred while fetching companies.");
            return View("List", new List<CompanyViewModel>());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CompanyViewModel model)
        {
            if (!ModelState.IsValid) return View("Modify", model);

            var client = GetClient();
            var response = await client.PostAsJsonAsync("/api/organization/companies", model);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, "An error occurred while creating the company.");
            return View("Modify", model);
        }

        private async Task PopulateParentCompaniesDropDownList(Guid? excludeId = null, object? selectedValue = null)
        {
            var client = GetClient();
            var allCompanies = await client.GetFromJsonAsync<List<CompanyViewModel>>("/api/organization/companies");
            var selectableCompanies = allCompanies?.Where(c => c.CompanyId != excludeId).ToList() ?? new List<CompanyViewModel>();
            ViewBag.ParentId = new SelectList(selectableCompanies, "CompanyId", "Name", selectedValue);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateParentCompaniesDropDownList();
            return View("Modify", new CompanyViewModel());
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var client = GetClient();
            var company = await client.GetFromJsonAsync<CompanyViewModel>($"/api/organization/companies/{id}");
            if (company == null)
            {
                return NotFound();
            }
            await PopulateParentCompaniesDropDownList(id, company.ParentId);
            return View("Modify", company);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Guid id, CompanyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateParentCompaniesDropDownList(id, model.ParentId);
                return View("Modify", model);
            }

            var client = GetClient();
            var response = await client.PutAsJsonAsync($"/api/organization/companies/{id}", model);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, "An error occurred while updating the company.");
            return View("Modify", model);
        }
    }
}
