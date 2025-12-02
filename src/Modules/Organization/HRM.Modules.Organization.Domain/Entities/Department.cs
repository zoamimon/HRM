namespace HRM.Modules.Organization.Domain.Entities
{
    public class Department
    {
        public Guid DepartmentId { get; private set; }
        public string Name { get; private set; }

        public Guid CompanyId { get; private set; }
        public virtual Company Company { get; private set; }

        public Guid? ParentId { get; private set; }
        public virtual Department Parent { get; private set; }

        private readonly List<Department> _children = new();
        public virtual IReadOnlyCollection<Department> Children => _children.AsReadOnly();

        private readonly List<Position> _positions = new();
        public virtual IReadOnlyCollection<Position> Positions => _positions.AsReadOnly();

        private Department() { }

        public Department(Guid id, string name, Company company, Department parent = null)
        {
            DepartmentId = id;
            Name = name;
            Company = company;
            CompanyId = company.CompanyId;
            Parent = parent;
            ParentId = parent?.DepartmentId;
        }

        public void Update(string name, Guid companyId, Guid? parentId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Department name cannot be empty.", nameof(name));
            }
            Name = name;
            CompanyId = companyId;
            ParentId = parentId;
        }
    }
}
