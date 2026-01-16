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
///
/// Performance:
/// - Executes 2 database queries:
///   1. COUNT query for TotalCount
///   2. SELECT query for Items with OFFSET/FETCH
/// - Both queries use same WHERE clause (efficient)
/// - COUNT query is typically fast with proper indexes
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
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber < 1 or pageSize <= 0</exception>
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
}
