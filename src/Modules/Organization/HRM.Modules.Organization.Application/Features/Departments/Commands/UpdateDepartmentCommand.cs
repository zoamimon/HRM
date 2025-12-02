using MediatR;

namespace HRM.Modules.Organization.Application.Features.Departments.Commands
{
    public class UpdateDepartmentCommand : IRequest
    {
        public Guid DepartmentId { get; set; }
        public string Name { get; set; }
        public Guid CompanyId { get; set; }
        public Guid? ParentId { get; set; }
    }

    public class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public UpdateDepartmentCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
        {
            var department = await _context.Departments.FindAsync(request.DepartmentId);
            if (department == null) throw new Exception("Department not found.");

            var company = await _context.Companies.FindAsync(request.CompanyId);
            if (company == null) throw new Exception("Company not found.");

            department.Update(request.Name, request.CompanyId, request.ParentId);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
