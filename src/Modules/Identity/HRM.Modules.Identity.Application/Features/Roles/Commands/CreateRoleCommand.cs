using HRM.Modules.Identity.Application.DAL;
using HRM.Modules.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Roles.Commands
{
    public class CreateRoleCommand : IRequest<Guid>
    {
        public string Name { get; set; }
        public IEnumerable<int> PermissionIds { get; set; } = new List<int>();
    }

    public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Guid>
    {
        private readonly IIdentityDbContext _context;

        public CreateRoleCommandHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
        {
            var newRoleId = Guid.NewGuid();
            var newRole = new Role(newRoleId, request.Name);

            if (request.PermissionIds.Any())
            {
                var permissions = await _context.Permissions
                    .Where(p => request.PermissionIds.Contains(p.PermissionId))
                    .ToListAsync(cancellationToken);

                foreach (var permission in permissions)
                {
                    newRole.AddPermission(permission);
                }
            }

            await _context.Roles.AddAsync(newRole, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return newRole.RoleId;
        }
    }
}
