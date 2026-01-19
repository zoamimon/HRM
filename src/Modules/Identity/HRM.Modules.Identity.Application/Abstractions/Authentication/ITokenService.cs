using HRM.BuildingBlocks.Domain.Abstractions.Authentication;

namespace HRM.Modules.Identity.Application.Abstractions.Authentication;

/// <summary>
/// Service for JWT token generation and management.
/// Generates access tokens and refresh tokens for authentication.
///
/// Token Architecture:
///
/// 1. Access Token (JWT):
///    - Short-lived (15 minutes)
///    - Contains user claims (ID, roles, scope)
///    - Stateless (no server-side storage)
///    - Used for API authorization
///    - Transmitted in Authorization header
///
/// 2. Refresh Token:
///    - Long-lived (7 days)
///    - Random secure string (not JWT)
///    - Stored in database (RefreshTokens table)
///    - Used to obtain new access token
///    - Transmitted in secure HttpOnly cookie or secure storage
///
/// Token Flow:
/// <code>
/// LOGIN:
/// 1. User provides credentials
/// 2. Server validates credentials
/// 3. Generate access token (JWT) + refresh token (random)
/// 4. Store refresh token in database
/// 5. Return both tokens to client
///
/// API REQUESTS:
/// 1. Client sends access token in Authorization header
/// 2. Server validates JWT signature and expiration
/// 3. Extract claims (user ID, roles) from token
/// 4. Process request with user context
///
/// TOKEN REFRESH:
/// 1. Access token expires (after 15 minutes)
/// 2. Client sends refresh token to /refresh endpoint
/// 3. Server validates refresh token in database
/// 4. Generate new access token
/// 5. Optionally rotate refresh token (generate new one)
/// 6. Return new tokens to client
/// </code>
///
/// JWT Token Structure:
/// <code>
/// Header:
/// {
///   "alg": "HS256",
///   "typ": "JWT"
/// }
///
/// Payload (Claims):
/// {
///   "sub": "operator-guid",           // User identifier
///   "name": "admin",                  // Username
///   "email": "admin@hrm.com",         // Email
///   "UserType": "Operator",           // Operator or User
///   "Roles": "SystemAdmin,Manager",   // Comma-separated roles
///   "ScopeLevel": "Company",          // Only for Users
///   "EmployeeId": "employee-guid",    // Only for Users
///   "jti": "token-guid",              // Unique token ID
///   "iat": 1704067200,                // Issued at (Unix timestamp)
///   "exp": 1704068100,                // Expires at (Unix timestamp)
///   "iss": "HRM.Api",                 // Issuer
///   "aud": "HRM.Clients"              // Audience
/// }
///
/// Signature:
/// HMACSHA256(
///   base64UrlEncode(header) + "." + base64UrlEncode(payload),
///   secretKey
/// )
/// </code>
///
/// Security Best Practices:
///
/// 1. Secret Key Management:
/// <code>
/// // ❌ BAD - Hardcoded secret
/// var secretKey = "my-super-secret-key-123";
///
/// // ✅ GOOD - From configuration/environment
/// var secretKey = configuration["JwtSettings:SecretKey"];
/// // Stored in: appsettings.json, Environment Variables, Azure Key Vault
/// </code>
///
/// 2. Token Expiration:
/// - Access Token: 15 minutes (short-lived)
/// - Refresh Token: 7 days (long-lived but revocable)
/// - Balance security vs user experience
///
/// 3. HTTPS Required:
/// - Tokens transmitted over encrypted connection
/// - Prevents man-in-the-middle attacks
/// - TLS 1.2+ required in production
///
/// 4. Token Storage (Client-Side):
/// - Access Token: Memory or sessionStorage (short-term)
/// - Refresh Token: HttpOnly secure cookie (preferred) or secure storage
/// - Never localStorage (vulnerable to XSS)
///
/// Usage in Login:
/// <code>
/// public class LoginCommandHandler
/// {
///     private readonly ITokenService _tokenService;
///
///     public async Task&lt;Result&lt;LoginResult&gt;&gt; Handle(...)
///     {
///         // 1. Validate credentials
///         var @operator = await ValidateCredentialsAsync(command);
///         if (@operator is null)
///             return Result.Failure(new UnauthorizedError(...));
///
///         // 2. Generate access token (JWT)
///         var accessToken = _tokenService.GenerateAccessToken(@operator);
///
///         // 3. Generate refresh token (random secure string)
///         var refreshToken = _tokenService.GenerateRefreshToken();
///
///         // 4. Store refresh token in database
///         var refreshTokenEntity = RefreshToken.CreateForOperator(
///             @operator.Id,
///             refreshToken,
///             DateTime.UtcNow.AddDays(7),
///             command.DeviceName,
///             command.DeviceFingerprint,
///             command.IpAddress,
///             command.UserAgent
///         );
///         await _refreshTokenRepository.AddAsync(refreshTokenEntity);
///
///         // 5. Return tokens to client
///         return Result.Success(new LoginResult
///         {
///             AccessToken = accessToken.Token,
///             RefreshToken = refreshToken,
///             AccessTokenExpiresAt = accessToken.ExpiresAt,
///             RefreshTokenExpiresAt = refreshTokenEntity.GetExpiresAt()
///         });
///     }
/// }
/// </code>
///
/// Token Refresh Implementation:
/// <code>
/// public class RefreshTokenCommandHandler
/// {
///     public async Task&lt;Result&lt;RefreshResult&gt;&gt; Handle(...)
///     {
///         // 1. Validate refresh token
///         var storedToken = await _refreshTokenRepository.GetByTokenAsync(
///             command.RefreshToken
///         );
///
///         if (storedToken is null || !storedToken.IsActive())
///             return Result.Failure(new UnauthorizedError(...));
///
///         // 2. Load user
///         var @operator = await _operatorRepository.GetByIdAsync(
///             storedToken.GetOperatorId()!.Value
///         );
///
///         // 3. Generate new access token
///         var newAccessToken = _tokenService.GenerateAccessToken(@operator);
///
///         // 4. Optional: Rotate refresh token (recommended for security)
///         var newRefreshToken = _tokenService.GenerateRefreshToken();
///         storedToken.Revoke(command.IpAddress, newRefreshToken);
///
///         var newRefreshTokenEntity = RefreshToken.CreateForOperator(
///             @operator.Id,
///             newRefreshToken,
///             DateTime.UtcNow.AddDays(7),
///             command.DeviceName,
///             command.DeviceFingerprint,
///             command.IpAddress,
///             command.UserAgent
///         );
///         await _refreshTokenRepository.AddAsync(newRefreshTokenEntity);
///
///         // 5. Return new tokens
///         return Result.Success(new RefreshResult
///         {
///             AccessToken = newAccessToken.Token,
///             RefreshToken = newRefreshToken,
///             AccessTokenExpiresAt = newAccessToken.ExpiresAt
///         });
///     }
/// }
/// </code>
///
/// Multi-Device Support:
/// - Each device gets unique refresh token
/// - Stored separately in RefreshTokens table
/// - Can revoke individual devices
/// - User can see and manage all active sessions
///
/// Token Revocation:
/// - Mark refresh token as revoked in database
/// - User logs out or changes password → revoke all tokens
/// - Security breach → admin can revoke user's tokens
/// - Access tokens can't be revoked (wait for expiration or use token blacklist)
///
/// Configuration Example:
/// <code>
/// // appsettings.json
/// {
///   "JwtSettings": {
///     "SecretKey": "your-256-bit-secret-key-min-32-characters",
///     "Issuer": "HRM.Api",
///     "Audience": "HRM.Clients",
///     "AccessTokenExpiryMinutes": 15,
///     "RefreshTokenExpiryDays": 7
///   }
/// }
/// </code>
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a JWT access token for an authenticated user.
    /// Token contains claims for authorization and user identification.
    ///
    /// Claims Included:
    /// - sub: User/Operator ID (NameIdentifier)
    /// - name: Username
    /// - email: Email address
    /// - UserType: "Operator" or "User"
    /// - Roles: Comma-separated role names
    /// - ScopeLevel: Data visibility level (for Users only)
    /// - EmployeeId: Employee identifier (for Users only)
    /// - jti: Unique token identifier
    /// - iat: Issued at timestamp
    /// - exp: Expiration timestamp
    /// - iss: Issuer (HRM.Api)
    /// - aud: Audience (HRM.Clients)
    ///
    /// Token Lifetime:
    /// - 15 minutes (configurable in appsettings)
    /// - Short-lived for security
    /// - Client should refresh before expiration
    ///
    /// Signature:
    /// - HMAC-SHA256 algorithm
    /// - Signed with secret key from configuration
    /// - Verifiable by any server with same key
    ///
    /// Example Output:
    /// <code>
    /// {
    ///   Token: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///   ExpiresAt: DateTime(2024-01-15 10:30:00)
    /// }
    /// </code>
    ///
    /// Usage:
    /// <code>
    /// var accessToken = _tokenService.GenerateAccessToken(@operator);
    ///
    /// // Return to client
    /// return new LoginResponse
    /// {
    ///     AccessToken = accessToken.Token,
    ///     ExpiresAt = accessToken.ExpiresAt
    /// };
    ///
    /// // Client uses token in requests:
    /// Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
    /// </code>
    /// </summary>
    /// <param name="authenticatable">User or Operator entity implementing IAuthenticatable</param>
    /// <returns>Access token with expiration time</returns>
    AccessTokenResult GenerateAccessToken(IAuthenticatable authenticatable);

    /// <summary>
    /// Generates a cryptographically secure random refresh token.
    ///
    /// Token Properties:
    /// - 64 bytes (512 bits) of random data
    /// - Base64-encoded for transmission
    /// - Not a JWT (opaque token)
    /// - Must be stored in database
    /// - Cannot be validated without database lookup
    ///
    /// Token Lifetime:
    /// - 7 days (configurable)
    /// - Longer than access token
    /// - Can be revoked at any time
    ///
    /// Security:
    /// - Generated using RNGCryptoServiceProvider
    /// - Cryptographically secure randomness
    /// - Impossible to guess or predict
    /// - No user information embedded
    ///
    /// Storage:
    /// - Hashed before storing in database (optional but recommended)
    /// - Linked to user via RefreshTokens table
    /// - Includes device information for tracking
    ///
    /// Example Output:
    /// "8f3d7b2a9e1c4f6h5j8k7l3m2n9o4p6q1r5s8t2u7v3w9x1y4z6a3b8c2d5e7f"
    ///
    /// Usage:
    /// <code>
    /// var refreshToken = _tokenService.GenerateRefreshToken();
    ///
    /// // Store in database
    /// var tokenEntity = RefreshToken.CreateForUser(
    ///     userId,
    ///     refreshToken,                    // Plain token (or hash it)
    ///     DateTime.UtcNow.AddDays(7),     // Expiration
    ///     deviceName,
    ///     deviceFingerprint,
    ///     ipAddress,
    ///     userAgent
    /// );
    ///
    /// await _refreshTokenRepository.AddAsync(tokenEntity);
    ///
    /// // Return to client (store in HttpOnly cookie or secure storage)
    /// return refreshToken;
    /// </code>
    ///
    /// Token Rotation:
    /// When refresh token is used, optionally generate new one:
    /// <code>
    /// // Revoke old token
    /// oldToken.Revoke(ipAddress, newRefreshToken);
    ///
    /// // Generate and store new token
    /// var newRefreshToken = _tokenService.GenerateRefreshToken();
    /// var newTokenEntity = RefreshToken.CreateForUser(...);
    /// await _refreshTokenRepository.AddAsync(newTokenEntity);
    /// </code>
    /// </summary>
    /// <returns>Cryptographically secure random token string</returns>
    string GenerateRefreshToken();
}

/// <summary>
/// Result of access token generation containing JWT and expiration.
/// </summary>
public sealed record AccessTokenResult
{
    /// <summary>
    /// JWT access token string.
    /// Format: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0..."
    /// Client should send this in Authorization header: "Bearer {Token}"
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Token expiration date/time (UTC).
    /// Client should refresh token before this time.
    /// Typically 15 minutes from generation.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }
}
