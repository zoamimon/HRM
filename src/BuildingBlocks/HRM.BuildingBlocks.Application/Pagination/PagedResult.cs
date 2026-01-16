namespace HRM.BuildingBlocks.Application.Pagination;

/// <summary>
/// Represents a paginated result set with metadata
/// Used for returning paginated data from queries with total count and navigation info
///
/// Purpose:
/// - Avoid loading entire datasets into memory
/// - Provide consistent pagination across all queries
/// - Include metadata for UI pagination controls
/// - Support both offset-based and cursor-based pagination
///
/// Usage Example:
/// <code>
/// public class SearchEmployeesQuery : IPagedQuery&lt;EmployeeDto&gt;
/// {
///     public string? SearchTerm { get; init; }
///     public int PageNumber { get; init; } = 1;
///     public int PageSize { get; init; } = 20;
/// }
///
/// public class SearchEmployeesQueryHandler
/// {
///     public async Task&lt;PagedResult&lt;EmployeeDto&gt;&gt; Handle(...)
///     {
///         var query = _context.Employees
///             .Where(e => e.FullName.Contains(query.SearchTerm))
///             .OrderBy(e => e.LastName);
///
///         return await query.ToPagedResultAsync(
///             query.PageNumber,
///             query.PageSize,
///             cancellationToken
///         );
///     }
/// }
/// </code>
///
/// Performance Considerations:
/// - TotalCount query executed separately (can be expensive on large tables)
/// - Consider caching TotalCount for frequently accessed data
/// - Use database indexes on columns used in WHERE and ORDER BY
/// - For very large datasets (millions of rows), consider cursor-based pagination
///
/// UI Integration:
/// <code>
/// // API Response
/// {
///   "items": [...],
///   "totalCount": 1250,
///   "pageNumber": 3,
///   "pageSize": 20,
///   "totalPages": 63,
///   "hasPreviousPage": true,
///   "hasNextPage": true
/// }
///
/// // Generate page links
/// var prevLink = result.HasPreviousPage ? $"/api/employees?page={result.PageNumber - 1}" : null;
/// var nextLink = result.HasNextPage ? $"/api/employees?page={result.PageNumber + 1}" : null;
/// </code>
/// </summary>
/// <typeparam name="T">Type of items in the result set</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>
    /// The items in the current page
    /// Empty list if no items found
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>
    /// Total number of items across all pages
    /// Used to calculate TotalPages and show "X of Y results"
    ///
    /// Example: If there are 1,250 employees total, TotalCount = 1250
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Current page number (1-based)
    /// First page is 1, not 0
    ///
    /// Example: For page 3, PageNumber = 3
    /// </summary>
    public required int PageNumber { get; init; }

    /// <summary>
    /// Number of items per page
    /// Used to calculate skip/take and TotalPages
    ///
    /// Common values: 10, 20, 25, 50, 100
    /// Example: PageSize = 20 means 20 items per page
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// Total number of pages
    /// Calculated as: Ceiling(TotalCount / PageSize)
    ///
    /// Example: 1,250 items / 20 per page = 63 pages
    /// </summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalCount / (double)PageSize)
        : 0;

    /// <summary>
    /// Whether there is a previous page
    /// True if PageNumber > 1
    ///
    /// Use to enable/disable "Previous" button
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Whether there is a next page
    /// True if PageNumber < TotalPages
    ///
    /// Use to enable/disable "Next" button
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Index of the first item on current page (0-based)
    /// Used for "Showing items X-Y of Z"
    ///
    /// Example: Page 3 with PageSize 20 → FirstItemIndex = 40 (items 41-60)
    /// </summary>
    public int FirstItemIndex => (PageNumber - 1) * PageSize;

    /// <summary>
    /// Index of the last item on current page (0-based)
    /// Used for "Showing items X-Y of Z"
    ///
    /// Example: Page 3 with PageSize 20 and 12 items → LastItemIndex = 51 (item 52)
    /// </summary>
    public int LastItemIndex => FirstItemIndex + Items.Count - 1;

    /// <summary>
    /// Whether the result set is empty
    /// True if Items collection has no elements
    /// </summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// Factory method to create an empty paged result
    /// Used when no items match the query
    /// </summary>
    /// <param name="pageNumber">Requested page number</param>
    /// <param name="pageSize">Requested page size</param>
    /// <returns>Empty paged result with zero total count</returns>
    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 20)
    {
        return new PagedResult<T>
        {
            Items = Array.Empty<T>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Factory method to create a paged result from a full list
    /// WARNING: Only use for small datasets already in memory
    /// For large datasets, use ToPagedResultAsync extension method
    /// </summary>
    /// <param name="items">Full list of items</param>
    /// <param name="pageNumber">Page number to extract</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paged result with requested page</returns>
    public static PagedResult<T> FromList(List<T> items, int pageNumber, int pageSize)
    {
        var pagedItems = items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<T>
        {
            Items = pagedItems,
            TotalCount = items.Count,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
