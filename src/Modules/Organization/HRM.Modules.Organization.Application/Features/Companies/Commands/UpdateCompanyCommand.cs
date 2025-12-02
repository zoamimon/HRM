using HRM.Shared.Kernel.Exceptions;
using MediatR;

namespace HRM.Modules.Organization.Application.Features.Companies.Commands
{
    public class UpdateCompanyCommand : IRequest
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; }
        public Guid? ParentId { get; set; }
    }

    public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public UpdateCompanyCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
        {
            var company = await _context.Companies
                .FindAsync(new object[] { request.CompanyId }, cancellationToken);

            if (company == null)
            {
                throw new NotFoundException("Company not found.");
            }

            company.Update(request.Name, request.ParentId);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
