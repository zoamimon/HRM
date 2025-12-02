using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Application.Features.Departments.Queries
{
    public record DepartmentDto(Guid DepartmentId, string Name, Guid CompanyId, Guid? ParentId);

    public class GetAllDepartmentsQuery : IRequest<List<DepartmentDto>>
    {
    }

    public class GetAllDepartmentsQueryHandler : IRequestHandler<GetAllDepartmentsQuery, List<DepartmentDto>>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public GetAllDepartmentsQueryHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DepartmentDto>> Handle(GetAllDepartmentsQuery request, CancellationToken cancellationToken)
        {
            return await _context.Departments
                .Select(d => new DepartmentDto(d.DepartmentId, d.Name, d.CompanyId, d.ParentId))
                .ToListAsync(cancellationToken);
        }
    }
}
