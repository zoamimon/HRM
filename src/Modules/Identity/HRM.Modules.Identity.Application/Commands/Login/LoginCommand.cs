using HRM.BuildingBlocks.Application.Abstractions.Commands;
using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Application.Commands.Login;

/// <summary>
/// Command to authenticate operator and generate access/refresh tokens
///
/// CQRS Pattern:
/// - Command: Performs login operation (not pure query, creates session)
/// - Returns: LoginResponse with tokens on success, Error on failure
/// - Idempotent: No (creates new session each time)
///
/// Security Features:
/// - Generic error messages (prevent username enumeration)
/// - Account lockout after failed attempts (brute force protection)
/// - IP and UserAgent tracking (session audit trail)
/// - Remember Me support (extended session duration)
///
/// Validation:
/// - UsernameOrEmail: Required, 3-255 chars
/// - Password: Required, 1-255 chars (complexity not checked here)
/// - RememberMe: Optional boolean
///
/// Business Rules:
/// - Operator must exist and be Active status
/// - Password must match stored hash
/// - Account must not be locked
/// - Failed attempt increments counter
/// - Successful login:
///   * Resets failed attempt counter
///   * Updates LastLoginAtUtc
///   * Creates refresh token in database
///   * Returns access + refresh tokens
///
/// Remember Me Logic:
/// - False: Refresh token expires in 7 days (default)
/// - True: Refresh token expires in 30 days (extended)
///
/// Flow:
/// 1. Find operator by username or email
/// 2. Check account status (Active, not locked)
/// 3. Verify password against stored hash
/// 4. Handle failed login (increment counter, lock if needed)
/// 5. Generate JWT access token
/// 6. Generate refresh token (with Remember Me expiry)
/// 7. Store refresh token in database
/// 8. Return tokens + user info
///
/// Usage (API):
/// <code>
/// POST /api/identity/login
/// {
///   "usernameOrEmail": "admin",
///   "password": "Admin@123456",
///   "rememberMe": true
/// }
///
/// Response (200 OK):
/// {
///   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
///   "refreshToken": "8f3d7b2a9e1c4f6h...", // Or in HttpOnly cookie
///   "accessTokenExpiry": "2024-01-15T10:30:00Z",
///   "refreshTokenExpiry": "2024-02-14T10:15:00Z",
///   "user": {
///     "id": "3fa85f64-...",
///     "username": "admin",
///     "email": "admin@company.com",
///     "fullName": "System Admin"
///   }
/// }
/// </code>
/// </summary>
/// <param name="UsernameOrEmail">Username or email address for login</param>
/// <param name="Password">Password (plaintext, will be verified against hash)</param>
/// <param name="RememberMe">Extend session duration (7 days → 30 days)</param>
/// <param name="IpAddress">Client IP address (for audit and security)</param>
/// <param name="UserAgent">Client user agent (browser/device info)</param>
public sealed record LoginCommand(
    string UsernameOrEmail,
    string Password,
    bool RememberMe = false,
    string? IpAddress = null,
    string? UserAgent = null
) : IModuleCommand<LoginResponse>
{
    /// <summary>
    /// Module name for Unit of Work routing
    /// </summary>
    public string ModuleName => "Identity";
}

/// <summary>
/// Response DTO for successful login
/// Contains tokens and user information
///
/// Token Storage Strategy:
/// - Web: AccessToken in memory (JS), RefreshToken in HttpOnly cookie
/// - Mobile: Both tokens in secure storage
/// - Server-to-server: AccessToken in memory, no refresh token
///
/// Security Notes:
/// - Never log or display tokens
/// - Access token short-lived (15 min)
/// - Refresh token long-lived (7-30 days)
/// - Refresh token can be revoked
/// </summary>
public sealed record LoginResponse
{
    /// <summary>
    /// JWT access token for API authorization
    /// Format: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    /// Usage: Authorization: Bearer {AccessToken}
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Refresh token for obtaining new access tokens
    /// Format: Random secure string (Base64-encoded)
    /// Usage: Sent to /api/identity/refresh endpoint
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// When access token expires (UTC)
    /// Client should refresh before this time
    /// </summary>
    public required DateTime AccessTokenExpiry { get; init; }

    /// <summary>
    /// When refresh token expires (UTC)
    /// User must re-login after this time
    /// </summary>
    public required DateTime RefreshTokenExpiry { get; init; }

    /// <summary>
    /// Authenticated user information
    /// Displayed in UI, stored in client state
    /// </summary>
    public required UserInfo User { get; init; }
}

/// <summary>
/// User information returned after login
/// Safe to display in UI (no sensitive data)
/// </summary>
public sealed record UserInfo
{
    /// <summary>
    /// Operator ID (GUID)
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Username for display
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Email address
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Full name for display
    /// </summary>
    public required string FullName { get; init; }
}
