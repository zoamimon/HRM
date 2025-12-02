using MediatR;

namespace HRM.Modules.Personnel.Application.Features.Employees.Commands
{
    public class UpdateEmployeeCommand : IRequest
    {
        public Guid EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }

    public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand>
    {
        private readonly DAL.IPersonnelDbContext _context;

        public UpdateEmployeeCommandHandler(DAL.IPersonnelDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
        {
            var employee = await _context.Employees.FindAsync(request.EmployeeId);
            if (employee == null) throw new Exception("Employee not found.");

            employee.Update(request.FirstName, request.LastName, request.Email);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
