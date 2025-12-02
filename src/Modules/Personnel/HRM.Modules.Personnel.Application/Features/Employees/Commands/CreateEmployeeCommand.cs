using MediatR;

namespace HRM.Modules.Personnel.Application.Features.Employees.Commands
{
    public class CreateEmployeeCommand : IRequest<Guid>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }
}
