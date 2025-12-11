namespace HRM.Web.ViewModels
{
    // For listing roles
    public class RoleViewModel
    {
        public int RoleId { get; set; }
        public string Name { get; set; }
    }

    // For creating/editing a role
    public class RoleModificationViewModel
    {
        public int RoleId { get; set; }
        public string Name { get; set; }
        public List<AssignedPermissionViewModel> Permissions { get; set; } = new();
    }

    public class AssignedPermissionViewModel
    {
        public int PermissionId { get; set; }
        public string Name { get; set; }
        public bool IsAssigned { get; set; }
    }
}
