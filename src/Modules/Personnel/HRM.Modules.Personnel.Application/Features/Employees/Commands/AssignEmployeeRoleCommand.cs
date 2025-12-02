using HRM.Modules.Personnel.Application.DAL;
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
        private readonly IHttpClientFactory _httpClientFactory;

        public AssignEmployeeRoleCommandHandler(IPersonnelDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<Guid> Handle(AssignEmployeeRoleCommand request, CancellationToken cancellationToken)
        {
            var isValidAssignment = await IsValidAssignment(request.CompanyId, request.DepartmentId, request.PositionId);
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

        private async Task<bool> IsValidAssignment(Guid companyId, Guid departmentId, Guid positionId)
        {
            // In a real system, this might make a call to the Organization module API
            // For now, we'll assume it's always valid.
            // var client = _httpClientFactory.CreateClient("OrganizationApi"); 
            // var response = await client.GetAsync($"/api/organization/positions/validate?companyId={companyId}&departmentId={departmentId}&positionId={positionId}");
            // return response.IsSuccessStatusCode;
            return await Task.FromResult(true);
        }

        private class ValidationResponse
        {
            public bool IsValid { get; set; }
        }
    }
}
