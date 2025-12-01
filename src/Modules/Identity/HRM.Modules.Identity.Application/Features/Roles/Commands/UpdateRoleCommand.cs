using HRM.Modules.Identity.Application.DAL;
using HRM.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Roles.Commands
{
    public class UpdateRoleCommand : IRequest
    {
        public Guid RoleId { get; set; }
        public string Name { get; set; }
        public IEnumerable<int> PermissionIds { get; set; } = new List<int>();
    }

    public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand>
    {
        private readonly IIdentityDbContext _context;

        public UpdateRoleCommandHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _context.Roles
                .Include(r => r.Permissions)
                .SingleOrDefaultAsync(r => r.RoleId == request.RoleId, cancellationToken);

            if (role == null)
            {
                throw new NotFoundException("Role not found.");
            }

            role.UpdateName(request.Name);

            var existingPermissionIds = role.Permissions.Select(p => p.PermissionId).ToList();
            var permissionsToAdd = request.PermissionIds.Except(existingPermissionIds).ToList();
            var permissionsToRemove = existingPermissionIds.Except(request.PermissionIds).ToList();

            if (permissionsToRemove.Any())
            {
                var permissions = role.Permissions.Where(p => permissionsToRemove.Contains(p.PermissionId)).ToList();
                foreach (var permission in permissions)
                {
                    role.RemovePermission(permission);
                }
            }

            if (permissionsToAdd.Any())
            {
                var permissions = await _context.Permissions
                    .Where(p => permissionsToAdd.Contains(p.PermissionId))
                    .ToListAsync(cancellationToken);

                foreach (var permission in permissions)
                {
                    role.AddPermission(permission);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
