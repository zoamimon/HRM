using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Application.Features.Companies.Queries
{
    public record CompanyDto(Guid CompanyId, string Name, Guid? ParentId);

    public class GetAllCompaniesQuery : IRequest<List<CompanyDto>>
    {
    }

    public class GetAllCompaniesQueryHandler : IRequestHandler<GetAllCompaniesQuery, List<CompanyDto>>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public GetAllCompaniesQueryHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CompanyDto>> Handle(GetAllCompaniesQuery request, System.Threading.CancellationToken cancellationToken)
        {
            return await _context.Companies
                .Select(c => new CompanyDto(c.CompanyId, c.Name, c.ParentId))
                .ToListAsync(cancellationToken);
        }
    }
}
