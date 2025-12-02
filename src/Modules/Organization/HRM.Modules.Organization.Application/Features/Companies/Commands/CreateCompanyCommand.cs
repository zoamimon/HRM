using HRM.Modules.Organization.Domain.Entities;
using MediatR;

namespace HRM.Modules.Organization.Application.Features.Companies.Commands
{
    public class CreateCompanyCommand : IRequest<Guid>
    {
        public string Name { get; set; }
        public Guid? ParentId { get; set; }
    }

    public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, Guid>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public CreateCompanyCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
        {
            Company parentCompany = null;
            if (request.ParentId.HasValue)
            {
                parentCompany = await _context.Companies.FindAsync(new object[] { request.ParentId.Value }, cancellationToken);
                if (parentCompany == null)
                {
                    // Or handle this case as per your application's requirements
                    throw new Exception($"Parent company with Id {request.ParentId} not found.");
                }
            }

            var company = new Company(Guid.NewGuid(), request.Name, parentCompany);

            await _context.Companies.AddAsync(company, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return company.CompanyId;
        }
    }
}
