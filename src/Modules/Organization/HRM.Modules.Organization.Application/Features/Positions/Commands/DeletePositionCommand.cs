using MediatR;

namespace HRM.Modules.Organization.Application.Features.Positions.Commands
{
    public class DeletePositionCommand : IRequest
    {
        public Guid PositionId { get; set; }
    }

    public class DeletePositionCommandHandler : IRequestHandler<DeletePositionCommand>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public DeletePositionCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeletePositionCommand request, CancellationToken cancellationToken)
        {
            var position = await _context.Positions.FindAsync(request.PositionId);
            if (position != null)
            {
                _context.Positions.Remove(position);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
