using MediatR;

namespace HRM.Modules.Organization.Application.Features.Departments.Commands
{
    public class DeleteDepartmentCommand : IRequest
    {
        public Guid DepartmentId { get; set; }
    }

    public class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand>
    {
        private readonly DAL.IOrganizationDbContext _context;

        public DeleteDepartmentCommandHandler(DAL.IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
        {
            var department = await _context.Departments.FindAsync(request.DepartmentId);
            if (department != null)
            {
                _context.Departments.Remove(department);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
