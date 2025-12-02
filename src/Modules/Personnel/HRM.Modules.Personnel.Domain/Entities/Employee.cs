using HRM.Shared.Kernel.Domain;

namespace HRM.Modules.Personnel.Domain.Entities
{
    public class Employee : AggregateRoot
    {
        public Guid EmployeeId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }

        private readonly List<EmployeeCompanyAssignment> _assignments = new();
        public virtual IReadOnlyCollection<EmployeeCompanyAssignment> Assignments => _assignments.AsReadOnly();

        private Employee() { }

        public Employee(Guid id, string firstName, string lastName, string email)
        {
            EmployeeId = id;
            FirstName = firstName;
            LastName = lastName;
            Email = email;
        }

        public void Create()
        {
            var employeeCreatedEvent = new Events.EmployeeCreatedEvent(
                this.EmployeeId,
                this.Email,
                this.FirstName,
                this.LastName);

            this.AddDomainEvent(employeeCreatedEvent);
        }

        public void Update(string firstName, string lastName, string email)
        {
            FirstName = firstName;
            LastName = lastName;
            Email = email;
        }

        public void AddAssignment(Guid companyId, Guid departmentId, Guid positionId, bool isPrimary, DateTime startDate)
        {
            if (isPrimary)
            {
                var currentPrimary = _assignments.FirstOrDefault(a => a.IsPrimaryRole && a.EndDate == null);
                currentPrimary?.SetAsNonPrimary();
            }

            _assignments.Add(new EmployeeCompanyAssignment(this.EmployeeId, companyId, departmentId, positionId, isPrimary, startDate));
        }

        public void EndAssignment(Guid assignmentId, DateTime endDate)
        {
            var assignment = _assignments.FirstOrDefault(a => a.Id == assignmentId);
            if (assignment != null)
            {
                assignment.EndAssignment(endDate);
            }
        }
    }
}
