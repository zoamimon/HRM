using MediatR;

namespace HRM.Modules.Personnel.Application.Features.Employees.Commands
{
    public class DeleteEmployeeCommand : IRequest
    {
        public Guid EmployeeId { get; set; }
    }

    public class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand>
    {
        private readonly DAL.IPersonnelDbContext _context;

        public DeleteEmployeeCommandHandler(DAL.IPersonnelDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
        {
            var employee = await _context.Employees.FindAsync(request.EmployeeId);
            if (employee != null)
            {
                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
