using HRM.BuildingBlocks.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace HRM.BuildingBlocks.Infrastructure.Extensions;

/// <summary>
/// Extension methods for IQueryable to support pagination
/// Provides convenient methods to convert queries to PagedResult
///
/// Purpose:
/// - Simplify pagination in query handlers
/// - Efficient database pagination with Skip/Take
/// - Separate TotalCount and Items queries
/// - Support async execution
/// - Protect against DOS attacks with MaxPageSize limit
///
/// Performance:
/// - Executes 2 database queries:
///   1. COUNT query for TotalCount
///   2. SELECT query for Items with OFFSET/FETCH
/// - Both queries use same WHERE clause (efficient)
/// - COUNT query is typically fast with proper indexes
///
/// Security:
/// - MaxPageSize limit (100 items) prevents DOS attacks
/// - Requesting large page sizes will throw ArgumentOutOfRangeException
/// - Protects database and application resources
///
/// Usage:
/// <code>
/// // In query handler
/// var query = _context.Employees
///     .Where(e => e.IsActive)
///     .OrderBy(e => e.LastName);
///
/// var result = await query.ToPagedResultAsync(
///     pageNumber: 1,
///     pageSize: 20,
///     cancellationToken
/// );
/// </code>
///
/// Generated SQL (approximate):
/// <code>
/// -- Count query
/// SELECT COUNT(*) FROM Employees WHERE IsActive = 1
///
/// -- Items query
/// SELECT * FROM Employees
/// WHERE IsActive = 1
/// ORDER BY LastName
/// OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
/// </code>
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Maximum allowed page size to prevent DOS attacks
    /// Requests exceeding this limit will throw ArgumentOutOfRangeException
    ///
    /// Rationale:
    /// - Large page sizes consume excessive memory (OOM risk)
    /// - Database query timeout for large result sets
    /// - Network bandwidth abuse
    /// - Poor UX (user can't process 1000+ items on screen)
    ///
    /// Configuration:
    /// - Default: 100 items per page
    /// - Adjust based on:
    ///   * Average entity size (smaller entities = allow more)
    ///   * Database performance
    ///   * Network constraints
    ///   * UI/UX requirements
    ///
    /// Example Impact:
    /// - 100 entities × 1KB each = 100KB response
    /// - 1000 entities × 1KB each = 1MB response (risky)
    /// - 10000 entities × 1KB each = 10MB response (DOS attack)
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Converts IQueryable to PagedResult with async execution
    /// Executes 2 queries: COUNT for total, SELECT for items
    ///
    /// Example:
    /// <code>
    /// var employees = _context.Employees
    ///     .Where(e => e.Department == "IT")
    ///     .OrderBy(e => e.LastName);
    ///
    /// var pagedResult = await employees.ToPagedResultAsync(1, 20);
    /// // Returns: 20 employees from page 1 + total count
    /// </code>
    ///
    /// Validation:
    /// - PageNumber must be >= 1 (validated before calling)
    /// - PageSize must be > 0 (validated before calling)
    /// - Query should have ORDER BY for consistent pagination
    ///
    /// Performance Tips:
    /// - Apply filters BEFORE calling this method
    /// - Ensure indexes on WHERE and ORDER BY columns
    /// - For very large tables, consider caching TotalCount
    /// - Use AsNoTracking() for read-only queries
    ///
    /// Warning:
    /// If query doesn't have ORDER BY, pagination results may be inconsistent
    /// Always apply ordering before pagination:
    /// <code>
    /// // ❌ BAD - No ordering, inconsistent results
    /// await query.ToPagedResultAsync(1, 20);
    ///
    /// // ✅ GOOD - Explicit ordering
    /// await query.OrderBy(e => e.Id).ToPagedResultAsync(1, 20);
    /// </code>
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="query">IQueryable to paginate</param>
    /// <param name="pageNumber">Page number (1-based, must be >= 1)</param>
    /// <param name="pageSize">Items per page (must be > 0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PagedResult with items and metadata</returns>
    /// <exception cref="ArgumentNullException">If query is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber < 1 or pageSize <= 0 or pageSize > MaxPageSize</exception>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "Page number must be at least 1"
            );
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                "Page size must be greater than 0"
            );
        }

        if (pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Page size cannot exceed {MaxPageSize}. " +
                $"Large page sizes can cause performance issues and DOS attacks. " +
                $"Consider implementing cursor-based pagination for large datasets."
            );
        }

        // Query 1: Get total count
        // Uses COUNT(*) which is optimized by database
        var totalCount = await query.CountAsync(cancellationToken);

        // Early return if no items
        if (totalCount == 0)
        {
            return PagedResult<T>.Empty(pageNumber, pageSize);
        }

        // Query 2: Get items for current page
        // Uses OFFSET/FETCH (SQL Server) or LIMIT/OFFSET (PostgreSQL)
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Converts IQueryable to PagedResult with projection (Select)
    /// Useful when you need to transform entities to DTOs before pagination
    ///
    /// Example:
    /// <code>
    /// var pagedResult = await _context.Employees
    ///     .Where(e => e.IsActive)
    ///     .OrderBy(e => e.LastName)
    ///     .ToPagedResultAsync(
    ///         pageNumber: 1,
    ///         pageSize: 20,
    ///         selector: e => new EmployeeDto
    ///         {
    ///             Id = e.Id,
    ///             FullName = e.FirstName + " " + e.LastName,
    ///             Email = e.Email
    ///         },
    ///         cancellationToken
    ///     );
    /// </code>
    ///
    /// Performance:
    /// - Projection happens in database (efficient)
    /// - Only selected columns returned from database
    /// - Reduces memory usage and network traffic
    ///
    /// Generated SQL:
    /// <code>
    /// SELECT Id, FirstName + ' ' + LastName AS FullName, Email
    /// FROM Employees
    /// WHERE IsActive = 1
    /// ORDER BY LastName
    /// OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY
    /// </code>
    /// </summary>
    /// <typeparam name="TSource">Source entity type</typeparam>
    /// <typeparam name="TResult">Result DTO type</typeparam>
    /// <param name="query">IQueryable to paginate</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Items per page</param>
    /// <param name="selector">Projection function (entity → DTO)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PagedResult with projected items</returns>
    /// <exception cref="ArgumentNullException">If query or selector is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber < 1 or pageSize <= 0 or pageSize > MaxPageSize</exception>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(
        this IQueryable<TSource> query,
        int pageNumber,
        int pageSize,
        System.Linq.Expressions.Expression<Func<TSource, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number must be at least 1");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than 0");
        }

        if (pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Page size cannot exceed {MaxPageSize}. " +
                $"Large page sizes can cause performance issues and DOS attacks. " +
                $"Consider implementing cursor-based pagination for large datasets."
            );
        }

        // Get total count from source query
        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<TResult>.Empty(pageNumber, pageSize);
        }

        // Apply projection and pagination
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResult>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Converts IQueryable to CursorPagedResult with async execution
    /// Implements cursor-based pagination for large datasets
    ///
    /// Cursor-based pagination is more efficient than offset-based for:
    /// - Large datasets (millions of rows)
    /// - Frequently changing data (inserts/deletes)
    /// - Infinite scroll UX
    /// - Activity feeds, logs, social media
    ///
    /// How It Works:
    /// 1. Filter: WHERE cursor_field > @cursor (or < for descending)
    /// 2. Fetch N+1 items (pageSize + 1 to check hasNext)
    /// 3. Return first N items
    /// 4. Set hasNext = true if fetched N+1 items
    /// 5. Extract cursor from last item
    ///
    /// Example Usage:
    /// <code>
    /// // First page (no cursor)
    /// var page1 = await _context.Activities
    ///     .Where(a => a.EmployeeId == employeeId)
    ///     .OrderByDescending(a => a.CreatedAt)
    ///     .ToCursorPagedResultAsync(
    ///         cursor: null,
    ///         pageSize: 20,
    ///         cursorSelector: a => a.CreatedAt.ToString("O"), // ISO 8601
    ///         cursorFilter: (query, cursor) => query.Where(a => a.CreatedAt < DateTime.Parse(cursor)),
    ///         cancellationToken
    ///     );
    ///
    /// // Next page (use cursor from previous)
    /// var page2 = await _context.Activities
    ///     .Where(a => a.EmployeeId == employeeId)
    ///     .OrderByDescending(a => a.CreatedAt)
    ///     .ToCursorPagedResultAsync(
    ///         cursor: page1.NextCursor,
    ///         pageSize: 20,
    ///         cursorSelector: a => a.CreatedAt.ToString("O"),
    ///         cursorFilter: (query, cursor) => query.Where(a => a.CreatedAt < DateTime.Parse(cursor)),
    ///         cancellationToken
    ///     );
    /// </code>
    ///
    /// Performance Comparison:
    /// - Offset-based: SELECT * FROM Table ORDER BY Id OFFSET 100000 LIMIT 20 (slow - scans 100K rows)
    /// - Cursor-based: SELECT * FROM Table WHERE Id > 100000 ORDER BY Id LIMIT 20 (fast - index seek)
    ///
    /// Important:
    /// - Query MUST have ORDER BY clause
    /// - Cursor column MUST be indexed
    /// - Cursor column MUST be unique or combined with unique column (e.g., Id)
    /// - cursorFilter lambda must match ORDER BY direction
    ///
    /// Sorting Direction:
    /// - ORDER BY Id ASC → WHERE Id > @cursor
    /// - ORDER BY Id DESC → WHERE Id < @cursor
    /// - ORDER BY CreatedAt DESC, Id DESC → WHERE (CreatedAt, Id) < (@cursor_date, @cursor_id)
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="query">IQueryable to paginate (must have ORDER BY)</param>
    /// <param name="cursor">Current cursor position (null for first page)</param>
    /// <param name="pageSize">Items per page (validated against MaxPageSize)</param>
    /// <param name="cursorSelector">Function to extract cursor value from entity</param>
    /// <param name="cursorFilter">Function to apply cursor filter to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cursor-based paged result</returns>
    /// <exception cref="ArgumentNullException">If query, cursorSelector, or cursorFilter is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageSize invalid</exception>
    public static async Task<CursorPagedResult<T>> ToCursorPagedResultAsync<T>(
        this IQueryable<T> query,
        string? cursor,
        int pageSize,
        Func<T, string> cursorSelector,
        Func<IQueryable<T>, string, IQueryable<T>> cursorFilter,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (cursorSelector is null)
        {
            throw new ArgumentNullException(nameof(cursorSelector));
        }

        if (cursorFilter is null)
        {
            throw new ArgumentNullException(nameof(cursorFilter));
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                "Page size must be greater than 0"
            );
        }

        if (pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Page size cannot exceed {MaxPageSize}"
            );
        }

        // Apply cursor filter if cursor provided
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            query = cursorFilter(query, cursor);
        }

        // Fetch N+1 items to check if there's a next page
        var items = await query
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        // Determine if there are more items
        var hasNext = items.Count > pageSize;

        // Take only requested page size
        var pageItems = hasNext ? items.Take(pageSize).ToList() : items;

        // Extract next cursor from last item
        var nextCursor = hasNext && pageItems.Count > 0
            ? cursorSelector(pageItems[^1])
            : null;

        return CursorPagedResult<T>.FromList(
            items: pageItems,
            pageSize: pageSize,
            hasNext: hasNext,
            nextCursor: nextCursor
        );
    }

    /// <summary>
    /// Converts IQueryable to CursorPagedResult with projection
    /// Combines cursor-based pagination with DTO mapping
    ///
    /// Example:
    /// <code>
    /// var result = await _context.Activities
    ///     .Where(a => a.EmployeeId == employeeId)
    ///     .OrderByDescending(a => a.Id)
    ///     .ToCursorPagedResultAsync(
    ///         cursor: null,
    ///         pageSize: 20,
    ///         cursorSelector: dto => dto.Id.ToString(),
    ///         cursorFilter: (query, cursor) => query.Where(a => a.Id < Guid.Parse(cursor)),
    ///         selector: a => new ActivityDto
    ///         {
    ///             Id = a.Id,
    ///             Description = a.Description,
    ///             OccurredAt = a.OccurredAt
    ///         },
    ///         cancellationToken
    ///     );
    /// </code>
    /// </summary>
    /// <typeparam name="TSource">Source entity type</typeparam>
    /// <typeparam name="TResult">Result DTO type</typeparam>
    public static async Task<CursorPagedResult<TResult>> ToCursorPagedResultAsync<TSource, TResult>(
        this IQueryable<TSource> query,
        string? cursor,
        int pageSize,
        Func<TResult, string> cursorSelector,
        Func<IQueryable<TSource>, string, IQueryable<TSource>> cursorFilter,
        System.Linq.Expressions.Expression<Func<TSource, TResult>> selector,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (cursorSelector is null)
        {
            throw new ArgumentNullException(nameof(cursorSelector));
        }

        if (cursorFilter is null)
        {
            throw new ArgumentNullException(nameof(cursorFilter));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be greater than 0");
        }

        if (pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, $"Page size cannot exceed {MaxPageSize}");
        }

        // Apply cursor filter if provided
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            query = cursorFilter(query, cursor);
        }

        // Project and fetch N+1 items
        var items = await query
            .Select(selector)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasNext = items.Count > pageSize;
        var pageItems = hasNext ? items.Take(pageSize).ToList() : items;
        var nextCursor = hasNext && pageItems.Count > 0
            ? cursorSelector(pageItems[^1])
            : null;

        return CursorPagedResult<TResult>.FromList(
            items: pageItems,
            pageSize: pageSize,
            hasNext: hasNext,
            nextCursor: nextCursor
        );
    }
}
