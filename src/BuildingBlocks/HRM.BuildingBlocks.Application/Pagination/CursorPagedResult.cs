namespace HRM.BuildingBlocks.Application.Pagination;

/// <summary>
/// Cursor-based paginated result container
/// Alternative to offset-based pagination (PagedResult) for large datasets
///
/// Cursor-Based vs Offset-Based:
///
/// Offset-Based (OFFSET/LIMIT):
/// - Simple to implement and understand
/// - Page numbers easy for UI (page 1, 2, 3...)
/// - Inefficient for large offsets (skips all previous rows)
/// - Unstable with concurrent inserts/deletes (items may appear twice or be skipped)
/// - SQL: SELECT * FROM Table ORDER BY Id OFFSET 10000 LIMIT 20
///
/// Cursor-Based (WHERE > cursor):
/// - More efficient for large datasets (no row skipping)
/// - Stable with concurrent changes (cursor = last seen item)
/// - No concept of "total pages" (infinite scroll UX)
/// - Requires indexed column for cursor (Id, CreatedAt, etc.)
/// - SQL: SELECT * FROM Table WHERE Id > @cursor ORDER BY Id LIMIT 20
///
/// Use Cases:
/// - Offset-Based: Admin panels, search results with page numbers, small-medium datasets
/// - Cursor-Based: Social media feeds, activity logs, large datasets, infinite scroll
///
/// Example Implementation:
/// <code>
/// // First page (no cursor)
/// var firstPage = await _context.Employees
///     .OrderBy(e => e.Id)
///     .ToCursorPagedResultAsync(
///         cursor: null,
///         pageSize: 20,
///         e => e.Id, // Cursor selector
///         cancellationToken
///     );
///
/// // Next page (use cursor from previous page)
/// var nextPage = await _context.Employees
///     .OrderBy(e => e.Id)
///     .ToCursorPagedResultAsync(
///         cursor: firstPage.NextCursor,
///         pageSize: 20,
///         e => e.Id,
///         cancellationToken
///     );
/// </code>
///
/// Performance Comparison:
/// | Dataset Size | Offset-Based | Cursor-Based |
/// |--------------|--------------|--------------|
/// | 1K rows      | 5ms          | 5ms          |
/// | 100K rows    | 50ms         | 5ms          |
/// | 1M rows      | 500ms        | 5ms          |
/// | 10M rows     | 5000ms       | 5ms          |
///
/// Response Format:
/// <code>
/// {
///   "items": [...],
///   "pageSize": 20,
///   "hasNext": true,
///   "nextCursor": "eyJpZCI6MTAwfQ==",
///   "hasPrevious": false,
///   "previousCursor": null
/// }
/// </code>
/// </summary>
/// <typeparam name="T">Entity or DTO type</typeparam>
public sealed record CursorPagedResult<T>
{
    /// <summary>
    /// Items for current page
    /// Empty list if no items found
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Number of items requested per page
    /// Actual items returned may be less (last page)
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// Indicates if there are more items after current page
    /// True = call API again with NextCursor to get more items
    /// False = reached end of dataset
    /// </summary>
    public required bool HasNext { get; init; }

    /// <summary>
    /// Cursor for next page
    /// NULL if HasNext = false (no more pages)
    /// Pass this value to next API call to get next page
    ///
    /// Encoding:
    /// - Typically base64-encoded JSON: {"id": 100, "createdAt": "2024-01-01"}
    /// - Or simple string: "100"
    /// - Implementation-specific (opaque to client)
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Indicates if there are items before current page
    /// True = can navigate backwards
    /// False = at beginning of dataset
    ///
    /// Note: Backwards navigation is optional
    /// Many cursor implementations only support forward pagination
    /// </summary>
    public bool HasPrevious { get; init; }

    /// <summary>
    /// Cursor for previous page
    /// NULL if HasPrevious = false (at beginning)
    /// Pass this value to API call to navigate backwards
    ///
    /// Note: Implementing backwards pagination is complex:
    /// - Requires reversing sort order
    /// - Must track first item of current page
    /// - Consider if your use case really needs it
    /// </summary>
    public string? PreviousCursor { get; init; }

    /// <summary>
    /// Create empty result (no items found)
    /// </summary>
    /// <param name="pageSize">Page size requested</param>
    /// <returns>Empty cursor-based paged result</returns>
    public static CursorPagedResult<T> Empty(int pageSize)
    {
        return new CursorPagedResult<T>
        {
            Items = Array.Empty<T>(),
            PageSize = pageSize,
            HasNext = false,
            NextCursor = null,
            HasPrevious = false,
            PreviousCursor = null
        };
    }

    /// <summary>
    /// Create result from list of items
    /// </summary>
    /// <param name="items">Items for current page</param>
    /// <param name="pageSize">Page size requested</param>
    /// <param name="hasNext">True if more items available</param>
    /// <param name="nextCursor">Cursor for next page (if hasNext = true)</param>
    /// <param name="hasPrevious">True if can navigate backwards</param>
    /// <param name="previousCursor">Cursor for previous page (if hasPrevious = true)</param>
    /// <returns>Cursor-based paged result</returns>
    public static CursorPagedResult<T> FromList(
        IReadOnlyList<T> items,
        int pageSize,
        bool hasNext,
        string? nextCursor,
        bool hasPrevious = false,
        string? previousCursor = null)
    {
        return new CursorPagedResult<T>
        {
            Items = items,
            PageSize = pageSize,
            HasNext = hasNext,
            NextCursor = nextCursor,
            HasPrevious = hasPrevious,
            PreviousCursor = previousCursor
        };
    }
}
