using HRM.BuildingBlocks.Domain.Entities;
using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.Modules.Identity.Domain.Entities;

/// <summary>
/// Refresh Token entity for managing user sessions and token rotation
///
/// Purpose:
/// - Store refresh tokens for JWT authentication
/// - Enable token revocation (logout, security breach)
/// - Support multi-device sessions
/// - Track session information (IP, user agent, device)
/// - Implement token rotation for security
/// - **Support multiple user types (Operator, Employee) via polymorphic design**
///
/// Polymorphic Design:
/// - Uses UserType + PrincipalId instead of specific foreign keys
/// - Single table serves multiple user types (Operator, Employee, etc.)
/// - Enables unified session management across all user types
/// - Simplifies queries and reduces code duplication
///
/// Token Lifecycle:
/// 1. Created: When user logs in
/// 2. Active: IsActive = true, not expired, not revoked
/// 3. Used: When refreshing access token (optionally rotated)
/// 4. Revoked: When user logs out or security event occurs
/// 5. Expired: ExpiresAt passes, automatically inactive
///
/// Security Features:
/// - Token rotation: Old token replaced with new on refresh
/// - Revocation tracking: Who revoked, when, from where
/// - Replacement tracking: Chain of token rotations
/// - Device fingerprinting: Track sessions by device
/// - IP tracking: Security audit trail
///
/// Business Rules:
/// - One user (any type) can have multiple active refresh tokens (multi-device)
/// - Expired tokens cannot be used (checked via IsActive)
/// - Revoked tokens cannot be reactivated
/// - Token rotation creates audit trail (ReplacedByToken)
///
/// Related Entities:
/// - Operator: One-to-Many relationship (polymorphic via UserType.Operator)
/// - Employee: One-to-Many relationship (polymorphic via UserType.Employee)
///
/// Database:
/// - Table: Identity.RefreshTokens
/// - Indexes: Token (unique), (UserType, PrincipalId), ExpiresAt
/// - Soft delete: No (hard delete after expiration + grace period)
/// </summary>
public sealed class RefreshToken : Entity
{
    /// <summary>
    /// Type of user that owns this refresh token (Operator, Employee, etc.)
    /// Used as discriminator for polymorphic association
    ///
    /// Database:
    /// - Stored as TINYINT (1 byte)
    /// - Part of composite index: (UserType, PrincipalId)
    ///
    /// Usage:
    /// - Filter queries by user type
    /// - Determine which table PrincipalId references
    /// - JWT claims for authorization
    /// </summary>
    public UserType UserType { get; private set; }

    /// <summary>
    /// ID of the user (Operator, Employee, etc.) that owns this refresh token
    /// Polymorphic foreign key - references different tables based on UserType
    ///
    /// References:
    /// - UserType.Operator → [Identity].Operators.Id
    /// - UserType.Employee → [Personnel].Employees.Id
    ///
    /// Note: Database cannot enforce FK constraint (polymorphic limitation)
    /// Application must validate principal exists before creating token
    /// </summary>
    public Guid PrincipalId { get; private set; }

    /// <summary>
    /// The refresh token value (random secure string)
    ///
    /// Properties:
    /// - 64 bytes (512 bits) of cryptographically secure random data
    /// - Base64-encoded, URL-safe
    /// - Must be unique across all tokens
    /// - Opaque (no user information embedded)
    ///
    /// Storage:
    /// - Stored in plain text (alternatively can be hashed)
    /// - Compared directly during validation
    ///
    /// Security:
    /// - Generated via RandomNumberGenerator
    /// - Impossible to guess or predict
    /// - Must be transmitted over HTTPS only
    /// </summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>
    /// Token expiration date/time (UTC)
    ///
    /// Expiration Strategy:
    /// - Normal login: 7 days (configurable)
    /// - Remember Me: 30 days (configurable)
    /// - Enforced at validation time
    ///
    /// Expired Token Handling:
    /// - IsActive returns false
    /// - Cannot be used to refresh access token
    /// - Periodically cleaned up from database
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// When the token was revoked (UTC)
    /// NULL if token is still active
    ///
    /// Revocation Scenarios:
    /// - User logs out
    /// - Token rotation (refresh used)
    /// - Password changed
    /// - Security breach detected
    /// - Admin revokes all user sessions
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// IP address from which token was revoked
    /// Used for security audit trail
    /// </summary>
    public string? RevokedByIp { get; private set; }

    /// <summary>
    /// New token that replaced this one (token rotation)
    /// NULL if not replaced (e.g., explicit logout)
    ///
    /// Token Rotation:
    /// - When refresh token is used, generate new one
    /// - Old token is revoked with ReplacedByToken = new token
    /// - Creates audit chain for security tracking
    /// </summary>
    public string? ReplacedByToken { get; private set; }

    /// <summary>
    /// IP address from which token was created
    /// Used for device tracking and security
    /// </summary>
    public string CreatedByIp { get; private set; } = string.Empty;

    /// <summary>
    /// User agent (browser/device) that created the token
    /// Used for session identification in UI
    ///
    /// Example values:
    /// - "Mozilla/5.0 (Windows NT 10.0; Win64; x64)..."
    /// - "HRM-Mobile-App/1.2.3 (iOS 16.0)"
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Check if token is currently active and usable
    ///
    /// Conditions for Active:
    /// - Not revoked (RevokedAt is NULL)
    /// - Not expired (current time < ExpiresAt)
    ///
    /// Usage:
    /// if (!token.IsActive)
    ///     return Error("Invalid or expired refresh token");
    /// </summary>
    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;

    /// <summary>
    /// Check if token is expired (regardless of revocation)
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private RefreshToken()
    {
    }

    /// <summary>
    /// Create new refresh token for any user type (Operator, Employee, etc.)
    ///
    /// Factory Method Pattern:
    /// - Ensures all required fields are provided
    /// - Validates business rules
    /// - Sets default values
    /// - Cannot create invalid token
    ///
    /// Polymorphic Design:
    /// - Accepts UserType + PrincipalId instead of specific entity ID
    /// - Works for Operators, Employees, and future user types
    /// - Application must validate principal exists before calling
    ///
    /// Usage:
    /// <code>
    /// // For Operator
    /// var token = RefreshToken.Create(
    ///     userType: UserType.Operator,
    ///     principalId: operator.Id,
    ///     token: _tokenService.GenerateRefreshToken(),
    ///     expiresAt: DateTime.UtcNow.AddDays(7),
    ///     ipAddress: clientInfo.IpAddress,
    ///     userAgent: clientInfo.UserAgent
    /// );
    ///
    /// // For Employee
    /// var token = RefreshToken.Create(
    ///     userType: UserType.Employee,
    ///     principalId: employee.Id,
    ///     token: _tokenService.GenerateRefreshToken(),
    ///     expiresAt: DateTime.UtcNow.AddDays(7),
    ///     ipAddress: clientInfo.IpAddress,
    ///     userAgent: clientInfo.UserAgent
    /// );
    ///
    /// await _dbContext.RefreshTokens.AddAsync(token);
    /// await _dbContext.SaveChangesAsync();
    /// </code>
    /// </summary>
    /// <param name="userType">Type of user (Operator, Employee, etc.)</param>
    /// <param name="principalId">ID of the user who owns this token</param>
    /// <param name="token">Random secure token string</param>
    /// <param name="expiresAt">Expiration date/time (UTC)</param>
    /// <param name="ipAddress">IP address of the client</param>
    /// <param name="userAgent">User agent string (browser/device)</param>
    /// <returns>New RefreshToken instance</returns>
    public static RefreshToken Create(
        UserType userType,
        Guid principalId,
        string token,
        DateTime expiresAt,
        string? ipAddress,
        string? userAgent)
    {
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("Principal ID cannot be empty", nameof(principalId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or empty", nameof(token));
        }

        if (expiresAt <= DateTime.UtcNow)
        {
            throw new ArgumentException("Expiration date must be in the future", nameof(expiresAt));
        }

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserType = userType,
            PrincipalId = principalId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedByIp = ipAddress ?? "unknown",
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Revoke this refresh token
    ///
    /// Revocation is permanent - cannot be undone
    /// Token becomes immediately unusable
    ///
    /// Use Cases:
    /// - User logs out
    /// - Token rotation (refresh used)
    /// - Security event (password changed, breach detected)
    /// - Admin revokes user sessions
    ///
    /// Usage:
    /// <code>
    /// // Simple revocation (logout)
    /// token.Revoke(ipAddress);
    ///
    /// // Token rotation (refresh)
    /// var newToken = _tokenService.GenerateRefreshToken();
    /// token.Revoke(ipAddress, replacedByToken: newToken);
    ///
    /// await _dbContext.SaveChangesAsync();
    /// </code>
    /// </summary>
    /// <param name="ipAddress">IP address from which revocation was requested</param>
    /// <param name="replacedByToken">New token if this is token rotation (optional)</param>
    public void Revoke(string? ipAddress, string? replacedByToken = null)
    {
        if (RevokedAt.HasValue)
        {
            // Already revoked - idempotent operation
            return;
        }

        RevokedAt = DateTime.UtcNow;
        RevokedByIp = ipAddress ?? "unknown";
        ReplacedByToken = replacedByToken;
    }
}
