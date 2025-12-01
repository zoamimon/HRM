using HRM.Modules.Identity.Application.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Roles.Queries
{
    public class GetRoleByIdQuery : IRequest<RoleDetailsDto>
    {
        public Guid Id { get; set; }
    }

    public class RoleDetailsDto
    {
        public Guid RoleId { get; set; }
        public string Name { get; set; }
        public IEnumerable<int> PermissionIds { get; set; }
    }

    public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDetailsDto>
    {
        private readonly IIdentityDbContext _context;

        public GetRoleByIdQueryHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<RoleDetailsDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
        {
            var role = await _context.Roles
                .Include(r => r.Permissions)
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.RoleId == request.Id, cancellationToken);

            if (role == null)
            {
                return null;
            }

            return new RoleDetailsDto
            {
                RoleId = role.RoleId,
                Name = role.Name,
                PermissionIds = role.Permissions.Select(p => p.PermissionId)
            };
        }
    }
}
