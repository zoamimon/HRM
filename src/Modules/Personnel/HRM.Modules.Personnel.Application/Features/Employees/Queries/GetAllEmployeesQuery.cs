using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Personnel.Application.Features.Employees.Queries
{
    public record EmployeeDto(Guid EmployeeId, string FirstName, string LastName, string Email);

    public class GetAllEmployeesQuery : IRequest<List<EmployeeDto>>
    {
    }

    public class GetAllEmployeesQueryHandler : IRequestHandler<GetAllEmployeesQuery, List<EmployeeDto>>
    {
        private readonly DAL.IPersonnelDbContext _context;

        public GetAllEmployeesQueryHandler(DAL.IPersonnelDbContext context)
        {
            _context = context;
        }

        public async Task<List<EmployeeDto>> Handle(GetAllEmployeesQuery request, CancellationToken cancellationToken)
        {
            return await _context.Employees
                .Select(e => new EmployeeDto(e.EmployeeId, e.FirstName, e.LastName, e.Email))
                .ToListAsync(cancellationToken);
        }
    }
}
