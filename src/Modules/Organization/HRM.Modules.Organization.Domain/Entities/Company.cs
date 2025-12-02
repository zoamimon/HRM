namespace HRM.Modules.Organization.Domain.Entities
{
    public class Company
    {
        public Guid CompanyId { get; private set; }
        public string Name { get; private set; }

        public Guid? ParentId { get; private set; }
        public virtual Company Parent { get; private set; }

        private readonly List<Company> _children = new();
        public virtual IReadOnlyCollection<Company> Children => _children.AsReadOnly();

        private readonly List<Department> _departments = new();
        public virtual IReadOnlyCollection<Department> Departments => _departments.AsReadOnly();

        private Company() { }

        public Company(Guid id, string name, Company parent = null)
        {
            CompanyId = id;
            Name = name;
            Parent = parent;
            ParentId = parent?.CompanyId;
        }

        public void Update(string name, Guid? parentId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Company name cannot be empty.", nameof(name));
            }
            Name = name;
            ParentId = parentId;
        }
    }
}
