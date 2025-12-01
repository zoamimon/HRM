using HRM.Modules.Identity.Application.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Features.Roles.Queries
{
    public class GetAllRolesQuery : IRequest<IEnumerable<RoleDto>>
    {
    }

    public class RoleDto
    {
        public Guid RoleId { get; set; }
        public string Name { get; set; }
    }

    public class GetAllRolesQueryHandler : IRequestHandler<GetAllRolesQuery, IEnumerable<RoleDto>>
    {
        private readonly IIdentityDbContext _context;

        public GetAllRolesQueryHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<RoleDto>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken)
        {
            return await _context.Roles
                .Select(r => new RoleDto
                {
                    RoleId = r.RoleId,
                    Name = r.Name
                })
                .ToListAsync(cancellationToken);
        }
    }
}
