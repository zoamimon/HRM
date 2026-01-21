using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Application.Commands.RevokeAllSessionsExceptCurrent;

/// <summary>
/// Command to revoke all sessions except current device
/// Useful for security: "Logout all other devices"
///
/// CQRS Pattern:
/// - Command: Mutates state (revokes multiple tokens)
/// - Returns: Result with count of revoked sessions
/// - Idempotent: Yes (already revoked = skip)
///
/// Use Cases:
/// - Suspected account compromise
/// - Lost device - secure account by logging out all others
/// - Privacy - clear all old sessions
/// - Security best practice - periodic cleanup
///
/// Business Rules:
/// - Revokes ALL active sessions except current
/// - Current session identified by refresh token
/// - Cannot revoke without current token (security)
/// - Idempotent (already revoked = skip)
///
/// Security:
/// - Current token required (cannot accidentally logout self)
/// - OperatorId from authenticated context
/// - IP tracking for audit trail
/// - Bulk operation (atomic transaction)
///
/// Usage (API):
/// <code>
/// POST /api/identity/sessions/revoke-all-except-current
/// Authorization: Bearer {access_token}
/// Cookie: refreshToken={refresh_token}
///
/// Response (200 OK):
/// {
///   "message": "3 sessions were terminated",
///   "revokedCount": 3
/// }
/// </code>
/// </summary>
/// <param name="OperatorId">Current operator ID (from auth context)</param>
/// <param name="CurrentRefreshToken">Current refresh token to preserve</param>
/// <param name="IpAddress">IP address for audit trail</param>
public sealed record RevokeAllSessionsExceptCurrentCommand(
    Guid OperatorId,
    string CurrentRefreshToken,
    string? IpAddress = null
) : IModuleCommand<Result<RevokeAllSessionsResult>>
{
    /// <summary>
    /// Module name for Unit of Work routing
    /// </summary>
    public string ModuleName => "Identity";
}

/// <summary>
/// Result DTO for RevokeAllSessionsExceptCurrent
/// </summary>
public sealed record RevokeAllSessionsResult
{
    /// <summary>
    /// Number of sessions that were revoked
    /// </summary>
    public required int RevokedCount { get; init; }

    /// <summary>
    /// Human-readable message
    /// </summary>
    public required string Message { get; init; }
}
