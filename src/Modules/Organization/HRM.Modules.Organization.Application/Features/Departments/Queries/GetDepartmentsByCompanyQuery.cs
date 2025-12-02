using HRM.Modules.Organization.Application.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Application.Features.Departments.Queries
{
    public class GetDepartmentsByCompanyQuery : IRequest<IEnumerable<DepartmentDto>>
    {
        public Guid CompanyId { get; set; }
    }

    public class GetDepartmentsByCompanyQueryHandler : IRequestHandler<GetDepartmentsByCompanyQuery, IEnumerable<DepartmentDto>>
    {
        private readonly IOrganizationDbContext _context;

        public GetDepartmentsByCompanyQueryHandler(IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DepartmentDto>> Handle(GetDepartmentsByCompanyQuery request, CancellationToken cancellationToken)
        {
            return await _context.Departments
                .Where(d => d.CompanyId == request.CompanyId)
                .Select(d => new DepartmentDto(d.DepartmentId, d.Name, d.CompanyId, d.ParentId))
                .ToListAsync(cancellationToken);
        }
    }
}
