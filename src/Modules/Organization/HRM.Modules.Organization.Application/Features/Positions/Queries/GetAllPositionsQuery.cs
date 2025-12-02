using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Application.Features.Positions.Queries
{
    public record PositionDto(Guid PositionId, string Name, Guid DepartmentId);

    public class GetAllPositionsQuery : IRequest<List<PositionDto>>
    {
    }

    public class GetAllPositionsQueryHandler : IRequestHandler<GetAllPositionsQuery, List<PositionDto>>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public GetAllPositionsQueryHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PositionDto>> Handle(GetAllPositionsQuery request, CancellationToken cancellationToken)
        {
            return await _context.Positions
                .Select(p => new PositionDto(p.PositionId, p.Name, p.DepartmentId))
                .ToListAsync(cancellationToken);
        }
    }
}
