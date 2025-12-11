using Microsoft.AspNetCore.Mvc.Rendering;

namespace HRM.Web.ViewModels
{
    // View model for the main assignment management page
    public class EmployeeAssignmentViewModel
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public List<AssignmentDetailViewModel> Assignments { get; set; }
        public NewAssignmentViewModel NewAssignment { get; set; }
    }

    // View model for displaying a single assignment (current or past)
    public class AssignmentDetailViewModel
    {
        public Guid AssignmentId { get; set; }
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public string PositionName { get; set; }
        public bool IsPrimary { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }

    // View model for the "Add New Assignment" form
    public class NewAssignmentViewModel
    {
        public Guid SelectedCompanyId { get; set; }
        public Guid SelectedDepartmentId { get; set; }
        public Guid SelectedPositionId { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime StartDate { get; set; } = DateTime.Today;

        public SelectList Companies { get; set; }
        public SelectList Departments { get; set; }
        public SelectList Positions { get; set; }
    }
}
