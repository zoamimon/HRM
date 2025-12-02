using HRM.Shared.Kernel.Domain;

namespace HRM.Modules.Personnel.Domain.Events
{
    public class EmployeeCreatedEvent : DomainEvent
    {
        public Guid EmployeeId { get; }
        public string Email { get; }
        public string FirstName { get; }
        public string LastName { get; }

        public EmployeeCreatedEvent(Guid employeeId, string email, string firstName, string lastName)
        {
            EmployeeId = employeeId;
            Email = email;
            FirstName = firstName;
            LastName = lastName;
        }
    }
}
