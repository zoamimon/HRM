using HRM.Modules.Personnel.Application.DAL;
using HRM.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Personnel.Application.Features.Employees.Commands
{
    public class EndEmployeeAssignmentCommand : IRequest
    {
        public Guid EmployeeId { get; set; }
        public Guid AssignmentId { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class EndEmployeeAssignmentCommandHandler : IRequestHandler<EndEmployeeAssignmentCommand>
    {
        private readonly IPersonnelDbContext _context;

        public EndEmployeeAssignmentCommandHandler(IPersonnelDbContext context)
        {
            _context = context;
        }

        public async Task Handle(EndEmployeeAssignmentCommand request, CancellationToken cancellationToken)
        {
            var employee = await _context.Employees
                .Include(e => e.Assignments)
                .SingleOrDefaultAsync(e => e.EmployeeId == request.EmployeeId, cancellationToken);

            if (employee == null)
            {
                throw new NotFoundException("Employee not found.");
            }

            employee.EndAssignment(request.AssignmentId, request.EndDate);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
