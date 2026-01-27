using HRM.BuildingBlocks.Application.Abstractions.Queries;
using HRM.BuildingBlocks.Application.Pagination;
using HRM.Modules.Identity.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace HRM.Modules.Identity.Application.Queries.GetOperators;

/// <summary>
/// Handler for GetOperatorsQuery
/// Returns paginated list of operators with search and filter support
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
///
/// Architecture:
/// - Handler in Application layer (Use Case)
/// - Depends on IIdentityQueryContext (abstraction)
/// - Implementation (IdentityDbContext) in Infrastructure
/// </summary>
public sealed class GetOperatorsQueryHandler
    : IQueryHandler<GetOperatorsQuery, PagedResult<OperatorSummaryDto>>
{
    private readonly IIdentityQueryContext _context;

    public GetOperatorsQueryHandler(IIdentityQueryContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<OperatorSummaryDto>> Handle(
        GetOperatorsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Operators.AsNoTracking();

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
