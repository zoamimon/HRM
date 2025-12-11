using HRM.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRM.Web.Controllers
{
    public class EmployeeController : BaseController
    {
        public EmployeeController(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
        {
        }

        public async Task<IActionResult> Index()
        {
            var client = GetClient();
            var response = await client.GetAsync("/api/personnel/employees");
            if (response.IsSuccessStatusCode)
            {
                var employees = await response.Content.ReadFromJsonAsync<List<EmployeeViewModel>>();
                return View("List", employees);
            }
            return View("List", new List<EmployeeViewModel>());
        }

        public IActionResult Create()
        {
            return View("Modify", new EmployeeViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(EmployeeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Modify", model);
            }

            var client = GetClient();
            await client.PostAsJsonAsync("/api/personnel/employees", model);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var client = GetClient();
            var employee = await client.GetFromJsonAsync<EmployeeViewModel>($"/api/personnel/employees/{id}");
            if (employee == null) return NotFound();

            return View("Modify", employee);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Guid id, EmployeeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Modify", model);
            }

            var client = GetClient();
            await client.PutAsJsonAsync($"/api/personnel/employees/{id}", model);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = GetClient();
            await client.DeleteAsync($"/api/personnel/employees/{id}");
            return RedirectToAction(nameof(Index));
        }

        // --- Assignment Management Actions ---

        [HttpGet]
        public async Task<IActionResult> Assignments(Guid id)
        {
            var client = GetClient();

            // 1. Get Employee Details
            var employee = await client.GetFromJsonAsync<EmployeeViewModel>($"/api/personnel/employees/{id}");
            if (employee == null) return NotFound();

            // 2. Get All Assignments for the Employee (This endpoint doesn't exist yet, we'll need to add it)
            // For now, let's assume we have a way to get assignment data. We'll need to expand the API.
            // Let's create a placeholder list for now.
            var assignments = new List<AssignmentDetailViewModel>(); // Placeholder

            // 3. Prepare the form for a new assignment
            var newAssignment = new NewAssignmentViewModel
            {
                Companies = await GetCompaniesSelectListAsync(client)
            };

            var viewModel = new EmployeeAssignmentViewModel
            {
                EmployeeId = employee.EmployeeId,
                EmployeeName = $"{employee.FirstName} {employee.LastName}",
                Assignments = assignments,
                NewAssignment = newAssignment
            };

            return View("Assignments", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddAssignment(Guid employeeId, NewAssignmentViewModel model)
        {
            var client = GetClient();
            var command = new
            {
                EmployeeId = employeeId,
                CompanyId = model.SelectedCompanyId,
                DepartmentId = model.SelectedDepartmentId,
                PositionId = model.SelectedPositionId,
                IsPrimary = model.IsPrimary,
                StartDate = model.StartDate
            };

            await client.PostAsJsonAsync($"/api/personnel/employees/{employeeId}/assignments", command);
            return RedirectToAction("Assignments", new { id = employeeId });
        }

        // --- Helper methods for AJAX calls to populate dropdowns ---

        [HttpGet]
        public async Task<JsonResult> GetDepartmentsByCompany(Guid companyId)
        {
            var client = GetClient();
            var departments = await client.GetFromJsonAsync<List<DepartmentViewModel>>($"/api/organization/companies/{companyId}/departments");
            return Json(new SelectList(departments, "DepartmentId", "Name"));
        }

        [HttpGet]
        public async Task<JsonResult> GetPositionsByDepartment(Guid departmentId)
        {
            var client = GetClient();
            var positions = await client.GetFromJsonAsync<List<PositionViewModel>>($"/api/organization/departments/{departmentId}/positions");
            return Json(new SelectList(positions, "PositionId", "Name"));
        }

        private async Task<SelectList> GetCompaniesSelectListAsync(HttpClient client)
        {
            var companies = await client.GetFromJsonAsync<List<CompanyViewModel>>("/api/organization/companies");
            return new SelectList(companies, "CompanyId", "Name");
        }

        [HttpPost]
        public async Task<IActionResult> EndAssignment(Guid employeeId, Guid assignmentId)
        {
            var client = GetClient();
            var command = new { EndDate = DateTime.UtcNow };
            await client.PutAsJsonAsync($"/api/personnel/employees/{employeeId}/assignments/{assignmentId}/end", command);
            return RedirectToAction("Assignments", new { id = employeeId });
        }
    }
}
