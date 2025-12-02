using HRM.Modules.Personnel.Application.DAL;
using HRM.Modules.Personnel.Application.Services;
using HRM.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Personnel.Application.Features.Employees.Commands
{
    public class AssignEmployeeRoleCommand : IRequest<Guid>
    {
        public Guid EmployeeId { get; set; }
        public Guid CompanyId { get; set; }
        public Guid DepartmentId { get; set; }
        public Guid PositionId { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime StartDate { get; set; }
    }

    public class AssignEmployeeRoleCommandHandler : IRequestHandler<AssignEmployeeRoleCommand, Guid>
    {
        private readonly IPersonnelDbContext _context;
        private readonly IOrganizationService _organizationService;

        public AssignEmployeeRoleCommandHandler(IPersonnelDbContext context, IOrganizationService organizationService)
        {
            _context = context;
            _organizationService = organizationService;
        }

        public async Task<Guid> Handle(AssignEmployeeRoleCommand request, CancellationToken cancellationToken)
        {
            var isValidAssignment = await _organizationService.IsValidAssignmentAsync(request.CompanyId, request.DepartmentId, request.PositionId);
            if (!isValidAssignment)
            {
                throw new ValidationException(new List<string> { "The provided combination of Company, Department, and Position is not valid." });
            }

            var employee = await _context.Employees
                .Include(e => e.Assignments)
                .SingleOrDefaultAsync(e => e.EmployeeId == request.EmployeeId, cancellationToken);

            if (employee == null)
            {
                throw new NotFoundException("Employee not found.");
            }

            employee.AddAssignment(request.CompanyId, request.DepartmentId, request.PositionId, request.IsPrimary, request.StartDate);

            await _context.SaveChangesAsync(cancellationToken);

            var newAssignment = employee.Assignments.OrderByDescending(a => a.StartDate).First();
            return newAssignment.Id;
        }
    }
}
