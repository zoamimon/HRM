using HRM.BuildingBlocks.Application.Pagination;
using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.Modules.Identity.Application.Queries.GetOperators;

/// <summary>
/// Query to retrieve paginated list of operators
/// Supports search by username/email and filter by status
///
/// Sort: CreatedAtUtc DESC (newest first)
/// </summary>
public sealed record GetOperatorsQuery : IPagedQuery<OperatorSummaryDto>
{
    /// <summary>
    /// Search term to filter by username or email (case-insensitive, contains match)
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Filter by operator status (null = all statuses)
    /// </summary>
    public OperatorStatus? Status { get; init; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page (default: 20, max: 100)
    /// </summary>
    public int PageSize { get; init; } = 20;
}
