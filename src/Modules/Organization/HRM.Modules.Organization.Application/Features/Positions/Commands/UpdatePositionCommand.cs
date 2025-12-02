using MediatR;

namespace HRM.Modules.Organization.Application.Features.Positions.Commands
{
    public class UpdatePositionCommand : IRequest
    {
        public Guid PositionId { get; set; }
        public string Name { get; set; }
        public Guid DepartmentId { get; set; }
    }

    public class UpdatePositionCommandHandler : IRequestHandler<UpdatePositionCommand>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public UpdatePositionCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdatePositionCommand request, CancellationToken cancellationToken)
        {
            var position = await _context.Positions.FindAsync(request.PositionId);
            if (position == null) throw new Exception("Position not found.");

            var department = await _context.Departments.FindAsync(request.DepartmentId);
            if (department == null) throw new Exception("Department not found.");

            position.Update(request.Name, request.DepartmentId);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
