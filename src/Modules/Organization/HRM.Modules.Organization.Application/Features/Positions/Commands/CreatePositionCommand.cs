using HRM.Modules.Organization.Domain.Entities;
using MediatR;

namespace HRM.Modules.Organization.Application.Features.Positions.Commands
{
    public class CreatePositionCommand : IRequest<Guid>
    {
        public string Name { get; set; }
        public Guid DepartmentId { get; set; }
    }

    public class CreatePositionCommandHandler : IRequestHandler<CreatePositionCommand, Guid>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public CreatePositionCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreatePositionCommand request, CancellationToken cancellationToken)
        {
            var department = await _context.Departments.FindAsync(request.DepartmentId);
            if (department == null) throw new Exception("Department not found");

            var position = new Position(Guid.NewGuid(), request.Name, department);

            await _context.Positions.AddAsync(position, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return position.PositionId;
        }
    }
}
