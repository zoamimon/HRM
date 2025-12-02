using MediatR;

namespace HRM.Modules.Organization.Application.Features.Departments.Queries
{
    public class GetDepartmentByIdQuery : IRequest<DepartmentDto>
    {
        public Guid DepartmentId { get; set; }
    }

    public class GetDepartmentByIdQueryHandler : IRequestHandler<GetDepartmentByIdQuery, DepartmentDto>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public GetDepartmentByIdQueryHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<DepartmentDto> Handle(GetDepartmentByIdQuery request, CancellationToken cancellationToken)
        {
            var department = await _context.Departments
                .FindAsync(new object[] { request.DepartmentId }, cancellationToken);

            if (department == null)
            {
                return null;
            }

            return new DepartmentDto(department.DepartmentId, department.Name, department.CompanyId, department.ParentId);
        }
    }
}
