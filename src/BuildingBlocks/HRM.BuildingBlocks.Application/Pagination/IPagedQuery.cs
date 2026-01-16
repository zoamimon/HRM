using HRM.BuildingBlocks.Application.Abstractions.Queries;

namespace HRM.BuildingBlocks.Application.Pagination;

/// <summary>
/// Marker interface for queries that return paginated results
/// Enforces pagination parameters on query objects
///
/// Purpose:
/// - Standardize pagination across all queries
/// - Ensure all paginated queries have PageNumber and PageSize
/// - Enable generic pagination handling in behaviors or middleware
/// - Provide type safety for paginated query handlers
///
/// Usage Pattern:
/// <code>
/// // Query inherits from IPagedQuery
/// public sealed record SearchEmployeesQuery : IPagedQuery&lt;EmployeeDto&gt;
/// {
///     // Query-specific filters
///     public string? SearchTerm { get; init; }
///     public string? Department { get; init; }
///     public EmployeeStatus? Status { get; init; }
///
///     // Pagination parameters (required by IPagedQuery)
///     public int PageNumber { get; init; } = 1;
///     public int PageSize { get; init; } = 20;
/// }
///
/// // Handler returns PagedResult
/// public class SearchEmployeesQueryHandler
///     : IQueryHandler&lt;SearchEmployeesQuery, PagedResult&lt;EmployeeDto&gt;&gt;
/// {
///     public async Task&lt;PagedResult&lt;EmployeeDto&gt;&gt; Handle(
///         SearchEmployeesQuery query,
///         CancellationToken cancellationToken)
///     {
///         var dbQuery = _context.Employees.AsQueryable();
///
///         // Apply filters
///         if (!string.IsNullOrEmpty(query.SearchTerm))
///             dbQuery = dbQuery.Where(e => e.FullName.Contains(query.SearchTerm));
///
///         // Apply pagination
///         return await dbQuery.ToPagedResultAsync(
///             query.PageNumber,
///             query.PageSize,
///             cancellationToken
///         );
///     }
/// }
/// </code>
///
/// Validation:
/// Queries implementing IPagedQuery should validate:
/// - PageNumber >= 1 (first page is 1, not 0)
/// - PageSize > 0 and PageSize <= MaxPageSize (e.g., 100)
///
/// Example Validator:
/// <code>
/// public class SearchEmployeesQueryValidator : AbstractValidator&lt;SearchEmployeesQuery&gt;
/// {
///     public SearchEmployeesQueryValidator()
///     {
///         RuleFor(x => x.PageNumber)
///             .GreaterThanOrEqualTo(1)
///             .WithMessage("Page number must be at least 1");
///
///         RuleFor(x => x.PageSize)
///             .GreaterThan(0)
///             .WithMessage("Page size must be greater than 0")
///             .LessThanOrEqualTo(100)
///             .WithMessage("Page size cannot exceed 100");
///     }
/// }
/// </code>
///
/// Default Values:
/// Best practices for default values:
/// - PageNumber = 1 (first page)
/// - PageSize = 20 (common default, good balance)
/// - MaxPageSize = 100 (prevent abuse)
///
/// API Integration:
/// <code>
/// // Query string parameters
/// GET /api/employees?searchTerm=John&pageNumber=2&pageSize=20
///
/// // Controller action
/// [HttpGet]
/// public async Task&lt;ActionResult&lt;PagedResult&lt;EmployeeDto&gt;&gt;&gt; Search(
///     [FromQuery] SearchEmployeesQuery query)
/// {
///     var result = await _mediator.Send(query);
///     return Ok(result);
/// }
/// </code>
///
/// Performance Considerations:
/// - Limit MaxPageSize to prevent large result sets
/// - Use database indexes on columns used in WHERE and ORDER BY
/// - Consider caching for frequently accessed pages
/// - For very large datasets, consider cursor-based pagination
///
/// Alternative Pagination:
/// For cursor-based pagination (better for real-time data):
/// <code>
/// public interface ICursorPagedQuery&lt;TResponse&gt; : IQuery&lt;CursorPagedResult&lt;TResponse&gt;&gt;
/// {
///     string? Cursor { get; init; }
///     int PageSize { get; init; }
/// }
/// </code>
/// </summary>
/// <typeparam name="TResponse">Type of items in paginated result</typeparam>
public interface IPagedQuery<TResponse> : IQuery<PagedResult<TResponse>>
{
    /// <summary>
    /// Current page number (1-based)
    /// First page is 1, not 0
    ///
    /// Validation:
    /// - Must be >= 1
    /// - Should have default value of 1
    ///
    /// Example: Page 1, 2, 3, ...
    /// </summary>
    int PageNumber { get; init; }

    /// <summary>
    /// Number of items per page
    ///
    /// Validation:
    /// - Must be > 0
    /// - Should be <= MaxPageSize (e.g., 100)
    /// - Should have default value (e.g., 20)
    ///
    /// Common values: 10, 20, 25, 50, 100
    /// </summary>
    int PageSize { get; init; }
}
