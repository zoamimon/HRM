namespace HRM.Modules.Organization.Domain.Entities
{
    public class Position
    {
        public Guid PositionId { get; private set; }
        public string Name { get; private set; }

        public Guid DepartmentId { get; private set; }
        public virtual Department Department { get; private set; }

        private Position() { }

        public Position(Guid id, string name, Department department)
        {
            PositionId = id;
            Name = name;
            Department = department;
            DepartmentId = department.DepartmentId;
        }

        public void Update(string name, Guid departmentId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Position name cannot be empty.", nameof(name));
            }
            Name = name;
            DepartmentId = departmentId;
        }
    }
}
