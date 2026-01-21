using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Application.Commands.Logout;

/// <summary>
/// Command to log out operator by revoking refresh token
///
/// CQRS Pattern:
/// - Command: Mutates state (revokes refresh token in database)
/// - Returns: Result (Success or Error)
/// - Idempotent: Yes (multiple logouts have same effect)
///
/// Logout Strategy:
/// - Revoke refresh token in database (immediate invalidation)
/// - Access token remains valid until expiry (stateless JWT)
/// - Client should discard both tokens immediately
///
/// Access Token Note:
/// - Cannot revoke access token (stateless JWT)
/// - Will expire naturally (15 minutes default)
/// - Short expiry mitigates security risk
/// - Alternative: Implement token blacklist (complex, not recommended)
///
/// Business Rules:
/// - Refresh token is revoked (RevokedAt set)
/// - IP address recorded for audit
/// - Idempotent: Already revoked tokens return success
/// - Invalid/expired tokens return success (no error)
///
/// Usage (API):
/// <code>
/// POST /api/identity/logout
/// Authorization: Bearer {access_token}
/// Cookie: refreshToken={refresh_token}
///
/// Response (200 OK):
/// {
///   "message": "Logged out successfully"
/// }
/// </code>
/// </summary>
/// <param name="RefreshToken">Refresh token to revoke</param>
/// <param name="IpAddress">IP address for audit trail</param>
public sealed record LogoutCommand(
    string RefreshToken,
    string? IpAddress = null
) : IModuleCommand<Result>
{
    /// <summary>
    /// Module name for Unit of Work routing
    /// </summary>
    public string ModuleName => "Identity";
}
