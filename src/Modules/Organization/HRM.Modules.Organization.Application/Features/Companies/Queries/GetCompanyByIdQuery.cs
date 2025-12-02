using MediatR;

namespace HRM.Modules.Organization.Application.Features.Companies.Queries
{
    public class GetCompanyByIdQuery : IRequest<CompanyDto>
    {
        public Guid CompanyId { get; set; }
    }

    public class GetCompanyByIdQueryHandler : IRequestHandler<GetCompanyByIdQuery, CompanyDto>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public GetCompanyByIdQueryHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<CompanyDto> Handle(GetCompanyByIdQuery request, CancellationToken cancellationToken)
        {
            var company = await _context.Companies
                .FindAsync(new object[] { request.CompanyId }, cancellationToken);

            if (company == null)
            {
                return null; // Or throw NotFoundException
            }

            return new CompanyDto(company.CompanyId, company.Name, company.ParentId);
        }
    }
}
