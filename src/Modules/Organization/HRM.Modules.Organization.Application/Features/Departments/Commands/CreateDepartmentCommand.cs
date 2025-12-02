using HRM.Modules.Organization.Domain.Entities;
using MediatR;

namespace HRM.Modules.Organization.Application.Features.Departments.Commands
{
    public class CreateDepartmentCommand : IRequest<Guid>
    {
        public string Name { get; set; }
        public Guid CompanyId { get; set; }
        public Guid? ParentId { get; set; }
    }

    public class CreateDepartmentCommandHandler : IRequestHandler<CreateDepartmentCommand, Guid>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public CreateDepartmentCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
        {
            // In a real app, you'd fetch the Company and parent Department entities
            var company = await _context.Companies.FindAsync(request.CompanyId);
            if (company == null) throw new Exception("Company not found");

            var department = new Department(Guid.NewGuid(), request.Name, company, null);

            await _context.Departments.AddAsync(department, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return department.DepartmentId;
        }
    }
}
