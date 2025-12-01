namespace HRM.Modules.Identity.Domain.Entities
{
    public class Permission
    {
        public int PermissionId { get; private set; }
        public string Name { get; private set; }

        // Private constructor for EF Core
        private Permission() { }

        public Permission(int id, string name)
        {
            PermissionId = id;
            Name = name;
        }

        public static class Permissions
        {
            public const string Read = "Read";
            public const string Create = "Create";
            public const string Update = "Update";
            public const string Delete = "Delete";
        }
    }
}
