using HRM.Modules.Identity.Application.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Roles.Queries
{
    public class GetAllPermissionsQuery : IRequest<IEnumerable<PermissionDto>>
    {
    }

    public class PermissionDto
    {
        public int PermissionId { get; set; }
        public string Name { get; set; }
    }

    public class GetAllPermissionsQueryHandler : IRequestHandler<GetAllPermissionsQuery, IEnumerable<PermissionDto>>
    {
        private readonly IIdentityDbContext _context;

        public GetAllPermissionsQueryHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PermissionDto>> Handle(GetAllPermissionsQuery request, CancellationToken cancellationToken)
        {
            return await _context.Permissions
                .Select(p => new PermissionDto
                {
                    PermissionId = p.PermissionId,
                    Name = p.Name
                })
                .ToListAsync(cancellationToken);
        }
    }
}
