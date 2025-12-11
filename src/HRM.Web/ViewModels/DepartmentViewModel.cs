namespace HRM.Web.ViewModels
{
    public class DepartmentViewModel
    {
        public Guid DepartmentId { get; set; }
        public string Name { get; set; }
        public Guid CompanyId { get; set; }
        public Guid? ParentId { get; set; }
    }
}
