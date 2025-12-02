using HRM.Modules.Organization.Application.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Application.Features.Positions.Queries
{
    public class GetPositionsByDepartmentQuery : IRequest<IEnumerable<PositionDto>>
    {
        public Guid DepartmentId { get; set; }
    }

    public class GetPositionsByDepartmentQueryHandler : IRequestHandler<GetPositionsByDepartmentQuery, IEnumerable<PositionDto>>
    {
        private readonly IOrganizationDbContext _context;

        public GetPositionsByDepartmentQueryHandler(IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PositionDto>> Handle(GetPositionsByDepartmentQuery request, CancellationToken cancellationToken)
        {
            return await _context.Positions
                .Where(p => p.DepartmentId == request.DepartmentId)
                .Select(p => new PositionDto(p.PositionId, p.Name, p.DepartmentId))
                .ToListAsync(cancellationToken);
        }
    }
}
