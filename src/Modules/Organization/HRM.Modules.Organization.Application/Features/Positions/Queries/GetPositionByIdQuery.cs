using MediatR;

namespace HRM.Modules.Organization.Application.Features.Positions.Queries
{
    public class GetPositionByIdQuery : IRequest<PositionDto>
    {
        public Guid PositionId { get; set; }
    }

    public class GetPositionByIdQueryHandler : IRequestHandler<GetPositionByIdQuery, PositionDto>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public GetPositionByIdQueryHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<PositionDto> Handle(GetPositionByIdQuery request, CancellationToken cancellationToken)
        {
            var position = await _context.Positions
                .FindAsync(new object[] { request.PositionId }, cancellationToken);

            if (position == null)
            {
                return null;
            }

            return new PositionDto(position.PositionId, position.Name, position.DepartmentId);
        }
    }
}
