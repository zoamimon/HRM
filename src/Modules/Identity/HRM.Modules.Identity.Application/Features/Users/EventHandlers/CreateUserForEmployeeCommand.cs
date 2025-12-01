using MediatR;

namespace HRM.Modules.Identity.Application.Features.Users.EventHandlers
{
    public class CreateUserForEmployeeCommand : IRequest
    {
        public Guid EmployeeId { get; }
        public string Email { get; }
        public string FirstName { get; }
        public string LastName { get; }

        public CreateUserForEmployeeCommand(Guid employeeId, string email, string firstName, string lastName)
        {
            EmployeeId = employeeId;
            Email = email;
            FirstName = firstName;
            LastName = lastName;
        }
    }
}
