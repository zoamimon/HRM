namespace HRM.Modules.Identity.Domain.Entities
{
    public class Role
    {
        public Guid RoleId { get; private set; }
        public string Name { get; private set; }

        private readonly List<Permission> _permissions = new();
        public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

        private Role() { }

        public Role(Guid id, string name)
        {
            RoleId = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public void UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("Role name cannot be empty.", nameof(newName));
            }
            Name = newName;
        }

        public void AddPermission(Permission permission)
        {
            if (permission is not null && !_permissions.Any(p => p.PermissionId == permission.PermissionId))
            {
                _permissions.Add(permission);
            }
        }

        public void RemovePermission(Permission permission)
        {
            if (permission is not null)
            {
                _permissions.Remove(permission);
            }
        }
    }
}
