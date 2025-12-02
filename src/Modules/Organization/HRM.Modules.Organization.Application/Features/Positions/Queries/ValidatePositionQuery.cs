using HRM.Modules.Organization.Application.DAL;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Organization.Application.Features.Positions.Queries
{
    public class ValidatePositionQuery : IRequest<bool>
    {
        public Guid CompanyId { get; set; }
        public Guid DepartmentId { get; set; }
        public Guid PositionId { get; set; }
    }

    public class ValidatePositionQueryHandler : IRequestHandler<ValidatePositionQuery, bool>
    {
        private readonly IOrganizationDbContext _context;

        public ValidatePositionQueryHandler(IOrganizationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(ValidatePositionQuery request, CancellationToken cancellationToken)
        {
            var position = await _context.Positions
                .Include(p => p.Department)
                .SingleOrDefaultAsync(p => p.PositionId == request.PositionId, cancellationToken);

            if (position == null)
            {
                return false;
            }

            if (position.DepartmentId != request.DepartmentId)
            {
                return false;
            }

            if (position.Department.CompanyId != request.CompanyId)
            {
                return false;
            }

            return true;
        }
    }
}
