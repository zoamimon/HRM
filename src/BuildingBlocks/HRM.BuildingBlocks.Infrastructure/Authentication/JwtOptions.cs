namespace HRM.BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// Configuration options for JWT token generation
/// Loaded from appsettings.json via IOptions pattern
///
/// Configuration Example (appsettings.json):
/// <code>
/// {
///   "JwtSettings": {
///     "SecretKey": "your-super-secret-key-min-32-characters-long-for-hs256",
///     "Issuer": "HRM.Api",
///     "Audience": "HRM.Clients",
///     "AccessTokenExpiryMinutes": 15,
///     "RefreshTokenExpiryDays": 7
///   }
/// }
/// </code>
///
/// Registration (Program.cs):
/// <code>
/// services.Configure<JwtOptions>(
///     configuration.GetSection("JwtSettings")
/// );
/// </code>
///
/// Usage in TokenService:
/// <code>
/// public class TokenService : ITokenService
/// {
///     private readonly JwtOptions _jwtOptions;
///
///     public TokenService(IOptions<JwtOptions> jwtOptions)
///     {
///         _jwtOptions = jwtOptions.Value;
///     }
/// }
/// </code>
///
/// Security Best Practices:
///
/// 1. Secret Key:
///    - Minimum 32 characters (256 bits) for HMAC-SHA256
///    - Use cryptographically secure random string
///    - Store in: Environment Variables, Azure Key Vault, AWS Secrets Manager
///    - NEVER commit to source control
///    - Rotate periodically (requires invalidating all tokens)
///
/// 2. Production Configuration:
///    <code>
///    // Use environment variable in production
///    "JwtSettings": {
///      "SecretKey": "${JWT_SECRET_KEY}",  // From environment
///      "Issuer": "HRM.Production.Api",
///      "Audience": "HRM.Production.Clients"
///    }
///    </code>
///
/// 3. Generate Secret Key (PowerShell):
///    <code>
///    # Generate 256-bit (32 bytes) random key
///    [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
///    </code>
///
/// 4. Generate Secret Key (Bash):
///    <code>
///    openssl rand -base64 32
///    </code>
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Secret key for signing JWT tokens (HMAC-SHA256)
    ///
    /// Requirements:
    /// - Minimum 32 characters (256 bits)
    /// - Cryptographically secure random string
    /// - Keep secret, never expose in logs/responses
    ///
    /// Security:
    /// - Used to sign and verify JWT tokens
    /// - Anyone with this key can generate valid tokens
    /// - Compromise = attacker can impersonate any user
    ///
    /// Example:
    /// "8f3d7b2a9e1c4f6h5j8k7l3m2n9o4p6q1r5s8t2u7v3w9x1y4z6a3b8c2d5e7f"
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Token issuer (who issued the token)
    /// Identifies the authentication server
    ///
    /// Standard JWT claim: "iss"
    ///
    /// Validation:
    /// - Tokens must be issued by trusted issuer
    /// - Prevents tokens from other systems
    ///
    /// Examples:
    /// - "HRM.Api" (development)
    /// - "https://api.hrm.company.com" (production)
    /// </summary>
    public string Issuer { get; set; } = "HRM.Api";

    /// <summary>
    /// Token audience (who should accept the token)
    /// Identifies the intended recipients
    ///
    /// Standard JWT claim: "aud"
    ///
    /// Validation:
    /// - Tokens must be for correct audience
    /// - Prevents token reuse across systems
    ///
    /// Examples:
    /// - "HRM.Clients" (development)
    /// - "https://app.hrm.company.com" (production)
    /// </summary>
    public string Audience { get; set; } = "HRM.Clients";

    /// <summary>
    /// Access token expiration time in minutes
    /// Default: 15 minutes
    ///
    /// Short-lived for security:
    /// - Limits damage if token is stolen
    /// - User refreshes token automatically
    /// - Balance between security and UX
    ///
    /// Recommendations:
    /// - Development: 60 minutes (easier testing)
    /// - Production: 15 minutes (more secure)
    /// - High security: 5 minutes
    /// </summary>
    public int AccessTokenExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token expiration time in days
    /// Default: 7 days
    ///
    /// Long-lived for user convenience:
    /// - User doesn't need to re-login frequently
    /// - Can be revoked from database if compromised
    ///
    /// Recommendations:
    /// - Development: 30 days
    /// - Production: 7-14 days
    /// - High security: 1-3 days
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
