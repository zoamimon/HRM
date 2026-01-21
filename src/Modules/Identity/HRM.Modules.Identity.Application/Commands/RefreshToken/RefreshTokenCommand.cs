using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.Results;
using HRM.Modules.Identity.Application.Commands.Login;

namespace HRM.Modules.Identity.Application.Commands.RefreshToken;

/// <summary>
/// Command to refresh access token using refresh token
/// Implements token rotation pattern for security
///
/// CQRS Pattern:
/// - Command: Mutates state (creates new tokens, revokes old)
/// - Returns: LoginResponse with new tokens
/// - Idempotent: No (each call creates new tokens)
///
/// Token Rotation Strategy:
/// - Old refresh token is revoked immediately
/// - New refresh token is generated and stored
/// - New access token is generated
/// - Creates audit trail (ReplacedByToken)
///
/// Security Benefits:
/// - Detects token theft (reuse of revoked token)
/// - Limits token lifetime
/// - Provides audit trail
/// - Enables session tracking
///
/// Business Rules:
/// - Refresh token must exist in database
/// - Refresh token must be active (not revoked, not expired)
/// - Old token is revoked with replacement tracking
/// - New token inherits same expiry duration
/// - Operator must still be Active status
///
/// Flow:
/// 1. Validate refresh token exists and is active
/// 2. Load operator from token
/// 3. Check operator status (Active)
/// 4. Generate new access token (JWT)
/// 5. Generate new refresh token
/// 6. Revoke old token (mark as replaced)
/// 7. Store new refresh token
/// 8. Return new tokens
///
/// Usage (API):
/// <code>
/// POST /api/identity/refresh
/// Cookie: refreshToken={refresh_token}
/// // Or in body for mobile: { "refreshToken": "..." }
///
/// Response (200 OK):
/// {
///   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
///   "refreshToken": "new-random-token...",
///   "accessTokenExpiry": "2024-01-15T10:30:00Z",
///   "refreshTokenExpiry": "2024-02-14T10:15:00Z",
///   "user": { ... }
/// }
/// </code>
/// </summary>
/// <param name="RefreshToken">Current refresh token to be rotated</param>
/// <param name="IpAddress">Client IP for audit trail</param>
/// <param name="UserAgent">Client user agent for session tracking</param>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? IpAddress = null,
    string? UserAgent = null
) : IModuleCommand<Result<LoginResponse>>
{
    /// <summary>
    /// Module name for Unit of Work routing
    /// </summary>
    public string ModuleName => "Identity";
}
