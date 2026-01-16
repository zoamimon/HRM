using HRM.BuildingBlocks.Application.Abstractions.Queries;

namespace HRM.BuildingBlocks.Application.Pagination;

/// <summary>
/// Marker interface for cursor-based paginated queries
/// Extends IQuery to return CursorPagedResult instead of List
///
/// Purpose:
/// - Type-safe query definition for cursor-based pagination
/// - Enforces Cursor and PageSize properties
/// - Works with MediatR pipeline behaviors
/// - Alternative to IPagedQuery (offset-based)
///
/// When to Use:
/// - Large datasets (millions of rows)
/// - Infinite scroll UX
/// - Activity feeds, logs, social media feeds
/// - Real-time data with frequent inserts
///
/// When NOT to Use:
/// - Small datasets (thousands of rows)
/// - Need page numbers (1, 2, 3...)
/// - Need total count / total pages
/// - Users expect to jump to specific page
///
/// Example Implementation:
/// <code>
/// // Query definition
/// public sealed record GetEmployeeActivityFeedQuery : ICursorPagedQuery&lt;EmployeeActivityDto&gt;
/// {
///     public string? Cursor { get; init; }
///     public int PageSize { get; init; } = 20;
///     public Guid EmployeeId { get; init; }
/// }
///
/// // Query handler
/// public sealed class GetEmployeeActivityFeedQueryHandler
///     : IQueryHandler&lt;GetEmployeeActivityFeedQuery, CursorPagedResult&lt;EmployeeActivityDto&gt;&gt;
/// {
///     private readonly IDbContext _context;
///
///     public async Task&lt;Result&lt;CursorPagedResult&lt;EmployeeActivityDto&gt;&gt;&gt; Handle(
///         GetEmployeeActivityFeedQuery query,
///         CancellationToken cancellationToken)
///     {
///         // Parse cursor (or null for first page)
///         Guid? cursorId = ParseCursor(query.Cursor);
///
///         var result = await _context.Activities
///             .Where(a => a.EmployeeId == query.EmployeeId)
///             .Where(a => cursorId == null || a.Id &lt; cursorId) // Cursor filter
///             .OrderByDescending(a => a.Id) // Newest first
///             .ToCursorPagedResultAsync(
///                 cursor: cursorId,
///                 pageSize: query.PageSize,
///                 cursorSelector: a => a.Id,
///                 cancellationToken
///             );
///
///         return Result.Success(result);
///     }
///
///     private Guid? ParseCursor(string? cursor)
///     {
///         if (string.IsNullOrWhiteSpace(cursor))
///             return null;
///
///         // Decode base64 and parse JSON
///         // Or simple Guid.Parse(cursor)
///         return Guid.Parse(cursor);
///     }
/// }
/// </code>
///
/// Cursor Format Options:
///
/// 1. Simple (Single Field):
/// <code>
/// // Just encode the ID
/// Cursor: "100"
/// NextCursor: "120"
/// </code>
///
/// 2. Base64 JSON (Multiple Fields):
/// <code>
/// // Encode complex cursor
/// {
///   "id": 100,
///   "createdAt": "2024-01-01T00:00:00Z"
/// }
/// Cursor: "eyJpZCI6MTAwLCJjcmVhdGVkQXQiOiIyMDI0LTAxLTAxVDAwOjAwOjAwWiJ9"
/// </code>
///
/// 3. Opaque Token (Encrypted):
/// <code>
/// // Encrypted cursor (prevents tampering)
/// Cursor: "enc_3f4a9b2c1d5e6f7a8b9c0d1e2f3a4b5c"
/// </code>
///
/// UI Integration:
/// <code>
/// // React example
/// const [items, setItems] = useState([]);
/// const [cursor, setCursor] = useState(null);
/// const [hasMore, setHasMore] = useState(true);
///
/// const loadMore = async () => {
///   const response = await fetch(`/api/activities?cursor=${cursor}&pageSize=20`);
///   const data = await response.json();
///
///   setItems([...items, ...data.items]); // Append to existing
///   setCursor(data.nextCursor);
///   setHasMore(data.hasNext);
/// };
///
/// // Infinite scroll
/// &lt;InfiniteScroll
///   dataLength={items.length}
///   next={loadMore}
///   hasMore={hasMore}
///   loader={&lt;Loading /&gt;}
/// >
///   {items.map(item => &lt;ActivityCard key={item.id} {...item} />)}
/// &lt;/InfiniteScroll>
/// </code>
///
/// Validation:
/// <code>
/// public class GetEmployeeActivityFeedQueryValidator
///     : AbstractValidator&lt;GetEmployeeActivityFeedQuery&gt;
/// {
///     public GetEmployeeActivityFeedQueryValidator()
///     {
///         RuleFor(x => x.PageSize)
///             .GreaterThan(0)
///             .LessThanOrEqualTo(100) // MaxPageSize
///             .WithMessage("Page size must be between 1 and 100");
///
///         RuleFor(x => x.Cursor)
///             .Must(BeValidCursor)
///             .When(x => !string.IsNullOrEmpty(x.Cursor))
///             .WithMessage("Invalid cursor format");
///     }
///
///     private bool BeValidCursor(string? cursor)
///     {
///         if (string.IsNullOrWhiteSpace(cursor))
///             return true;
///
///         return Guid.TryParse(cursor, out _);
///     }
/// }
/// </code>
/// </summary>
/// <typeparam name="TResponse">DTO type returned in result items</typeparam>
public interface ICursorPagedQuery<TResponse> : IQuery<CursorPagedResult<TResponse>>
{
    /// <summary>
    /// Cursor for current page position
    /// NULL or empty = first page (start from beginning)
    /// Non-null = continue from this position
    ///
    /// Format:
    /// - Simple: "100" (just the ID)
    /// - Base64: "eyJpZCI6MTAwfQ==" (encoded JSON)
    /// - Opaque: "enc_xyz123" (encrypted token)
    ///
    /// Client Usage:
    /// - First request: cursor = null
    /// - Subsequent requests: cursor = response.nextCursor
    /// </summary>
    string? Cursor { get; init; }

    /// <summary>
    /// Number of items to return per page
    /// Must be between 1 and MaxPageSize (typically 100)
    ///
    /// Recommendations:
    /// - Mobile: 10-20 items (slower networks)
    /// - Desktop: 20-50 items
    /// - Infinite scroll: 20-30 items (smooth UX)
    ///
    /// Default: 20 items (good balance)
    /// </summary>
    int PageSize { get; init; }
}
