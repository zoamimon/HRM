using HRM.BuildingBlocks.Application.Abstractions.Queries;
using HRM.BuildingBlocks.Application.Pagination;
using HRM.Modules.Identity.Application.Queries.GetOperators;
using HRM.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Infrastructure.Queries;

/// <summary>
/// Handler for GetOperatorsQuery
/// Located in Infrastructure layer because it requires direct IdentityDbContext access
///
/// Query Logic:
/// 1. Apply search filter (username or email contains search term)
/// 2. Apply status filter if specified
/// 3. Sort by CreatedAtUtc DESC (newest first)
/// 4. Apply pagination (skip/take)
/// 5. Project to OperatorSummaryDto
///
/// Performance:
/// - Uses AsNoTracking for read-only query
/// - Projects directly to DTO (no entity materialization)
/// - Two queries: COUNT for total, SELECT for page data
/// - Indexed columns: Username, Email, Status, CreatedAtUtc
/// </summary>
internal sealed class GetOperatorsQueryHandler
    : IQueryHandler<GetOperatorsQuery, PagedResult<OperatorSummaryDto>>
{
    private readonly IdentityDbContext _dbContext;

    public GetOperatorsQueryHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<OperatorSummaryDto>> Handle(
        GetOperatorsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Operators.AsNoTracking();

        // Apply search filter (username or email contains search term)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(o =>
                o.Username.ToLower().Contains(searchTerm) ||
                o.Email.ToLower().Contains(searchTerm));
        }

        // Apply status filter
        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting and pagination
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OperatorSummaryDto
            {
                Id = o.Id,
                Username = o.Username,
                Email = o.Email,
                FullName = o.FullName,
                Status = o.Status,
                CreatedAtUtc = o.CreatedAtUtc,
                LastLoginAtUtc = o.LastLoginAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<OperatorSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
