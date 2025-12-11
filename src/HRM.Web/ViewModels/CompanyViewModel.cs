namespace HRM.Web.ViewModels
{
    public class CompanyViewModel
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; }
        public Guid? ParentId { get; set; }
    }
}
