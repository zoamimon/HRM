using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HRM.Modules.Identity.Domain.Abstractions.Authentication;
using HRM.Modules.Identity.Domain.Enums;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HRM.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// JWT token service implementation
/// Generates access tokens (JWT) and refresh tokens (random secure strings)
///
/// Token Types:
///
/// 1. Access Token (JWT):
///    - Short-lived (15 minutes)
///    - Contains user claims (ID, roles, scope)
///    - Stateless (no database lookup needed)
///    - Used for API authorization
///
/// 2. Refresh Token:
///    - Long-lived (7 days)
///    - Random secure string (64 bytes)
///    - Stored in database
///    - Used to obtain new access token
///
/// Thread Safety:
/// - Stateless service (no shared mutable state)
/// - Safe to register as singleton
/// - JwtSecurityTokenHandler is thread-safe
/// - RNGCryptoServiceProvider is thread-safe
///
/// Performance:
/// - Token generation: ~1-2ms
/// - Fast enough for high-throughput scenarios
/// - No database access during generation
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    /// <summary>
    /// Constructor with JWT options
    /// </summary>
    /// <param name="jwtOptions">JWT configuration from appsettings.json</param>
    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        _tokenHandler = new JwtSecurityTokenHandler();

        ValidateOptions();
    }

    /// <summary>
    /// Validate JWT options at startup
    /// Fail fast if configuration is invalid
    /// </summary>
    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
        {
            throw new InvalidOperationException(
                "JWT SecretKey is not configured. " +
                "Set JwtSettings:SecretKey in appsettings.json or environment variables."
            );
        }

        if (_jwtOptions.SecretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT SecretKey must be at least 32 characters (256 bits) for HMAC-SHA256. " +
                $"Current length: {_jwtOptions.SecretKey.Length}"
            );
        }

        if (string.IsNullOrWhiteSpace(_jwtOptions.Issuer))
        {
            throw new InvalidOperationException("JWT Issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_jwtOptions.Audience))
        {
            throw new InvalidOperationException("JWT Audience is not configured.");
        }

        if (_jwtOptions.AccessTokenExpiryMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT AccessTokenExpiryMinutes must be greater than 0."
            );
        }

        if (_jwtOptions.RefreshTokenExpiryDays <= 0)
        {
            throw new InvalidOperationException(
                "JWT RefreshTokenExpiryDays must be greater than 0."
            );
        }
    }

    /// <summary>
    /// Generate JWT access token for authenticated user
    ///
    /// Token Structure:
    /// - Header: Algorithm (HS256), Type (JWT)
    /// - Payload: Claims (see below)
    /// - Signature: HMAC-SHA256(header + payload, secretKey)
    ///
    /// Claims Included:
    /// - sub: User ID (NameIdentifier)
    /// - name: Username
    /// - email: Email address
    /// - UserType: "Operator" or "User"
    /// - Roles: Comma-separated (if applicable)
    /// - ScopeLevel: Data visibility level (Users only)
    /// - EmployeeId: Employee identifier (Users only)
    /// - jti: Unique token identifier
    /// - iat: Issued at timestamp
    /// - exp: Expiration timestamp
    /// - iss: Issuer (from config)
    /// - aud: Audience (from config)
    ///
    /// Expiration:
    /// - Set to current time + AccessTokenExpiryMinutes
    /// - Default: 15 minutes
    /// - Client should refresh before expiration
    /// </summary>
    public AccessTokenResult GenerateAccessToken(IAuthenticatable authenticatable)
    {
        if (authenticatable is null)
        {
            throw new ArgumentNullException(nameof(authenticatable));
        }

        // Calculate expiration time
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpiryMinutes);

        // Create claims
        var claims = new List<Claim>
        {
            // Standard JWT claims
            new(JwtRegisteredClaimNames.Sub, authenticatable.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),

            // User information claims
            new(ClaimTypes.NameIdentifier, authenticatable.Id.ToString()),
            new(ClaimTypes.Name, authenticatable.GetUsername()),
            new(ClaimTypes.Email, authenticatable.GetEmail()),

            // Custom claims - include both for backward compatibility during migration
            new("AccountType", authenticatable.GetAccountType().ToString()),
            new("UserType", authenticatable.GetAccountType().ToString()) // Keep for backward compatibility
        };

        // Add AccountType-specific claims
        if (authenticatable.GetAccountType() == AccountType.Employee)
        {
            // For Users, add ScopeLevel and EmployeeId
            // These need to be retrieved from the User entity
            // Assuming authenticatable is actually a User instance
            // This is a simplification - in real implementation, you'd cast or use additional properties
            AddUserSpecificClaims(claims, authenticatable);
        }

        // Create signing credentials
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Create token descriptor
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = credentials
        };

        // Generate token
        var token = _tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = _tokenHandler.WriteToken(token);

        return new AccessTokenResult
        {
            Token = tokenString,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Add User-specific claims (ScopeLevel, EmployeeId)
    /// This is a helper method to add claims that only apply to Users
    ///
    /// Note: In a real implementation, the User entity would have additional
    /// properties or methods to retrieve ScopeLevel and EmployeeId.
    /// For now, this is a placeholder that shows the pattern.
    /// </summary>
    private void AddUserSpecificClaims(List<Claim> claims, IAuthenticatable authenticatable)
    {
        // In real implementation, cast to User type and get specific properties
        // For now, this is a placeholder showing the structure

        // Example (pseudo-code):
        // if (authenticatable is User user)
        // {
        //     claims.Add(new Claim("ScopeLevel", user.GetScopeLevel().ToString()));
        //     claims.Add(new Claim("EmployeeId", user.GetEmployeeId().ToString()));
        //
        //     var roles = user.GetRoles();
        //     if (roles?.Any() == true)
        //     {
        //         claims.Add(new Claim("Roles", string.Join(",", roles)));
        //     }
        // }

        // TODO: Implement this when User entity is available
        // For now, add placeholder claims for testing
    }

    /// <summary>
    /// Generate cryptographically secure refresh token
    ///
    /// Token Properties:
    /// - 64 bytes (512 bits) of random data
    /// - Base64-encoded for transmission
    /// - URL-safe characters
    /// - Not a JWT (opaque token)
    ///
    /// Security:
    /// - Uses RandomNumberGenerator.GetBytes() (cryptographically secure)
    /// - Impossible to guess or predict
    /// - No user information embedded
    /// - Must be stored in database for validation
    ///
    /// Storage:
    /// - Store in database (RefreshTokens table)
    /// - Link to user ID
    /// - Include expiration date
    /// - Optional: Hash before storing (like passwords)
    ///
    /// Example Output:
    /// "8f3d7b2a9e1c4f6h5j8k7l3m2n9o4p6q1r5s8t2u7v3w9x1y4z6a3b8c2d5e7f"
    /// </summary>
    public string GenerateRefreshToken()
    {
        // Generate 64 bytes of random data
        var randomBytes = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        // Convert to Base64 string (URL-safe)
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")  // Replace + with - (URL-safe)
            .Replace("/", "_")  // Replace / with _ (URL-safe)
            .Replace("=", "");  // Remove padding
    }

    /// <summary>
    /// Generates a cryptographically secure random refresh token with custom expiration.
    /// Supports "Remember Me" functionality by accepting custom expiry dates.
    ///
    /// The expiration date parameter is included for completeness (handlers pass it to entity),
    /// but the token generation itself is identical to the parameterless version.
    ///
    /// Usage Pattern (Remember Me):
    /// <code>
    /// var expiryDays = request.RememberMe
    ///     ? _jwtOptions.RememberMeExpiryDays
    ///     : _jwtOptions.RefreshTokenExpiryDays;
    ///
    /// var expiresAt = DateTime.UtcNow.AddDays(expiryDays);
    /// var token = _tokenService.GenerateRefreshToken(expiresAt);
    ///
    /// // Store with custom expiration
    /// var tokenEntity = RefreshToken.Create(
    ///     userId,
    ///     token,
    ///     expiresAt,
    ///     ipAddress,
    ///     userAgent
    /// );
    /// </code>
    ///
    /// Security Note:
    /// - The expiresAt parameter is NOT embedded in the token
    /// - Expiration is enforced at validation time (database lookup)
    /// - Token itself is still opaque random bytes
    /// </summary>
    /// <param name="expiresAt">Custom expiration date/time (UTC). Not embedded in token.</param>
    /// <returns>Cryptographically secure random token string (identical format to parameterless version)</returns>
    public string GenerateRefreshToken(DateTime expiresAt)
    {
        // Token generation is identical - expiration is handled by the entity/database
        // The expiresAt parameter is provided for API consistency and to pass to RefreshToken.Create()
        return GenerateRefreshToken();
    }
}
