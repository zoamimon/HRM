using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Application.Commands.RevokeSession;

/// <summary>
/// Command to revoke a specific session (logout from specific device)
/// Allows user to logout from other devices remotely
///
/// CQRS Pattern:
/// - Command: Mutates state (revokes refresh token)
/// - Returns: Result (Success or Error)
/// - Idempotent: Yes (already revoked = success)
///
/// Use Cases:
/// - User sees suspicious login from unknown device
/// - Lost phone/laptop - revoke that session
/// - Session management - logout from old devices
/// - Security dashboard - "Logout this device"
///
/// Business Rules:
/// - Session must belong to current operator (security)
/// - Session ID from GetActiveSessionsQuery
/// - Revocation is permanent
/// - Idempotent (no error if already revoked)
///
/// Security:
/// - OperatorId from authenticated context
/// - Cannot revoke other users' sessions
/// - IP tracking for audit trail (auto-injected by AuditBehavior)
///
/// Audit Logging:
/// - Implements IAuditableCommand for automatic audit injection
/// - IpAddress and UserAgent automatically populated by AuditBehavior
/// - No need to pass audit parameters from API endpoint
///
/// Usage (API):
/// <code>
/// DELETE /api/identity/sessions/{sessionId}
/// Authorization: Bearer {access_token}
///
/// Response (204 No Content)
/// </code>
/// </summary>
/// <param name="SessionId">Session ID to revoke (RefreshToken.Id)</param>
/// <param name="OperatorId">Current operator ID (from auth context)</param>
public sealed record RevokeSessionCommand(
    Guid SessionId,
    Guid OperatorId
) : IModuleCommand<Result>, IAuditableCommand
{
    /// <summary>
    /// Module name for Unit of Work routing
    /// </summary>
    public string ModuleName => "Identity";

    /// <summary>
    /// IP address for audit trail (auto-injected by AuditBehavior)
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent for audit trail (auto-injected by AuditBehavior)
    /// </summary>
    public string? UserAgent { get; set; }
}
