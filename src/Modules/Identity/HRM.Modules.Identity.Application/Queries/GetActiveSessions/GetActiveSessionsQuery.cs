using HRM.BuildingBlocks.Application.Abstractions.Queries;
using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Application.Queries.GetActiveSessions;

/// <summary>
/// Query to get operator's active sessions (devices)
/// Displays all active refresh tokens for session management
///
/// CQRS Pattern:
/// - Query: Read-only, no state mutation
/// - Returns: List of SessionInfo
/// - Cacheable: No (session data changes frequently)
///
/// Use Cases:
/// - Display logged-in devices in user profile
/// - Security dashboard (where am I logged in?)
/// - Suspicious activity detection
/// - Session management UI
///
/// Business Rules:
/// - Only returns active sessions (not revoked, not expired)
/// - Sorted by most recent first
/// - Marks current session (if provided)
/// - User can only see their own sessions
///
/// Security:
/// - OperatorId from authenticated user context
/// - Cannot query other users' sessions
/// - Current token identification for UI hints
///
/// Performance:
/// - Indexed query (OperatorId, RevokedAt, ExpiresAt)
/// - Typically 1-10 rows per user
/// - Fast query (~1-5ms)
///
/// Usage (API):
/// <code>
/// GET /api/identity/sessions
/// Authorization: Bearer {access_token}
///
/// Response (200 OK):
/// [
///   {
///     "id": "3fa85f64-...",
///     "createdAt": "2024-01-15T10:00:00Z",
///     "expiresAt": "2024-02-14T10:00:00Z",
///     "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)...",
///     "createdByIp": "192.168.1.100",
///     "isCurrent": true
///   },
///   {
///     "id": "2ea74f53-...",
///     "createdAt": "2024-01-14T08:00:00Z",
///     "expiresAt": "2024-02-13T08:00:00Z",
///     "userAgent": "HRM-Mobile-App/1.2.3 (iOS 16.0)",
///     "createdByIp": "10.0.0.5",
///     "isCurrent": false
///   }
/// ]
/// </code>
/// </summary>
/// <param name="OperatorId">Operator ID (from authenticated context)</param>
/// <param name="CurrentRefreshToken">Current refresh token to mark as IsCurrent (optional)</param>
public sealed record GetActiveSessionsQuery(
    Guid OperatorId,
    string? CurrentRefreshToken = null
) : IQuery<Result<List<SessionInfo>>>;

/// <summary>
/// Session information DTO
/// Represents a single active session (device)
/// </summary>
public sealed record SessionInfo
{
    /// <summary>
    /// Session ID (RefreshToken.Id)
    /// Used for revocation
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// When session was created (UTC)
    /// Login timestamp
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When session expires (UTC)
    /// User must re-login after this
    /// </summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// User agent (browser/device)
    /// Parsed for display: "Chrome on Windows", "iOS App"
    /// </summary>
    public required string? UserAgent { get; init; }

    /// <summary>
    /// IP address where session created
    /// </summary>
    public required string CreatedByIp { get; init; }

    /// <summary>
    /// Is this the current session?
    /// True if this session's token matches CurrentRefreshToken
    /// Used for UI hints ("This device")
    /// </summary>
    public required bool IsCurrent { get; init; }
}
