using HRM.Modules.Personnel.Application.DAL;
using HRM.Modules.Personnel.Domain.Entities;
using MediatR;

namespace HRM.Modules.Personnel.Application.Features.Employees.Commands
{
    public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Guid>
    {
        private readonly IPersonnelDbContext _context;

        public CreateEmployeeCommandHandler(IPersonnelDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var employee = new Employee(
                Guid.NewGuid(),
                request.FirstName,
                request.LastName,
                request.Email);

            employee.Create();

            await _context.Employees.AddAsync(employee, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return employee.EmployeeId;
        }
    }
}
