using HRM.Modules.Identity.Application.Abstractions.Authentication;
using BCrypt.Net;

namespace HRM.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// BCrypt-based password hasher implementation
/// Uses BCrypt.Net-Next library for secure password hashing
///
/// BCrypt Features:
/// - Adaptive hashing (work factor can be increased over time)
/// - Automatic salt generation (unique per password)
/// - Constant-time verification (prevents timing attacks)
/// - Mature, battle-tested algorithm (used by major companies)
///
/// Work Factor Configuration:
/// - Default: 12 (2^12 = 4096 iterations)
/// - Time: ~100-200ms per hash (acceptable for login)
/// - Security: Adequate for current hardware (2024-2026)
/// - Future: Can increase to 13 or 14 as CPUs get faster
///
/// Hash Format:
/// $2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW
/// │ │  │        │                     └─ Hash (31 chars)
/// │ │  │        └─ Salt (22 chars, base64-encoded)
/// │ │  └─ Work factor (12 = 4096 rounds)
/// │ └─ BCrypt version (2a)
/// └─ Algorithm identifier ($)
///
/// Performance Characteristics:
/// - HashPassword: ~150ms (work factor 12)
/// - VerifyPassword: ~150ms (same as hash)
/// - Intentionally slow (prevents brute-force attacks)
///
/// Security Benefits:
/// - Rainbow table attacks: Prevented by unique salt per password
/// - Brute force attacks: Slowed by work factor
/// - Timing attacks: Prevented by constant-time comparison
/// - GPU cracking: Harder than MD5/SHA (but possible with specialized hardware)
///
/// Why BCrypt over alternatives:
/// - vs MD5/SHA: BCrypt is adaptive and salted (MD5/SHA are too fast)
/// - vs PBKDF2: BCrypt is more GPU-resistant
/// - vs Argon2: BCrypt is more mature, easier to deploy (Argon2 is more secure but newer)
///
/// Migration to Argon2:
/// If needed in future, can detect hash format and use appropriate algorithm:
/// <code>
/// if (passwordHash.StartsWith("$2a$") || passwordHash.StartsWith("$2b$"))
///     return BCrypt.Verify(password, passwordHash);
/// else if (passwordHash.StartsWith("$argon2"))
///     return Argon2.Verify(password, passwordHash);
/// </code>
///
/// Thread Safety:
/// - BCrypt.Net is thread-safe
/// - No shared state
/// - Safe to use as singleton
///
/// Usage Example:
/// <code>
/// // Registration
/// var hashedPassword = _passwordHasher.HashPassword("MySecurePassword123!");
/// // Store hashedPassword in database
///
/// // Login
/// bool isValid = _passwordHasher.VerifyPassword(
///     "MySecurePassword123!",  // User input
///     hashedPassword            // From database
/// );
/// </code>
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// BCrypt work factor (cost parameter)
    /// 12 = 2^12 = 4096 iterations
    ///
    /// Recommended values:
    /// - 10: Fast but less secure (~30-50ms)
    /// - 11: Balanced (~60-100ms)
    /// - 12: Recommended for production (~100-200ms) ← CURRENT
    /// - 13: More secure but slower (~200-400ms)
    /// - 14: High security (~400-800ms)
    ///
    /// Increase this value every few years as CPUs get faster
    /// </summary>
    private const int WorkFactor = 12;

    /// <summary>
    /// Hash a plaintext password using BCrypt
    ///
    /// Process:
    /// 1. Generate random 128-bit salt
    /// 2. Combine password + salt
    /// 3. Apply BCrypt with work factor 12
    /// 4. Return hash string (includes version, cost, salt, hash)
    ///
    /// Performance:
    /// - ~150ms per hash (work factor 12)
    /// - Acceptable for registration/password change
    /// - Do NOT call in loops
    ///
    /// Output:
    /// - 60 characters string
    /// - Contains all info needed for verification
    /// - Store this in database (VARCHAR(100) or larger for future algorithms)
    /// </summary>
    /// <param name="password">Plaintext password from user</param>
    /// <returns>BCrypt hash string</returns>
    /// <exception cref="ArgumentNullException">If password is null</exception>
    /// <exception cref="ArgumentException">If password is empty</exception>
    public string HashPassword(string password)
    {
        if (password is null)
        {
            throw new ArgumentNullException(nameof(password), "Password cannot be null");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty or whitespace", nameof(password));
        }

        // BCrypt.HashPassword automatically:
        // 1. Generates random salt
        // 2. Applies work factor
        // 3. Returns hash in standard format
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <summary>
    /// Verify a plaintext password against a BCrypt hash
    ///
    /// Process:
    /// 1. Extract salt and work factor from hash
    /// 2. Hash provided password with same salt + work factor
    /// 3. Compare hashes using constant-time comparison
    /// 4. Return true if match, false otherwise
    ///
    /// Performance:
    /// - ~150ms per verification (same as hashing)
    /// - Acceptable for login
    /// - Do NOT call in loops
    ///
    /// Security:
    /// - Constant-time comparison prevents timing attacks
    /// - No information leakage about stored hash
    /// - Safe against brute force (slow work factor)
    ///
    /// Important Notes:
    /// - Each hash has unique salt
    /// - Same password produces different hashes
    /// - Must use VerifyPassword, NOT direct string comparison
    /// - Hash format must be valid BCrypt ($2a$, $2b$, or $2y$)
    /// </summary>
    /// <param name="password">Plaintext password from user</param>
    /// <param name="passwordHash">BCrypt hash from database</param>
    /// <returns>True if password matches, false otherwise</returns>
    /// <exception cref="ArgumentNullException">If password or passwordHash is null</exception>
    /// <exception cref="ArgumentException">If password is empty or hash format is invalid</exception>
    public bool VerifyPassword(string password, string passwordHash)
    {
        if (password is null)
        {
            throw new ArgumentNullException(nameof(password), "Password cannot be null");
        }

        if (passwordHash is null)
        {
            throw new ArgumentNullException(nameof(passwordHash), "Password hash cannot be null");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty or whitespace", nameof(password));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash cannot be empty or whitespace", nameof(passwordHash));
        }

        try
        {
            // BCrypt.Verify automatically:
            // 1. Extracts salt from hash
            // 2. Hashes provided password with same salt
            // 3. Compares using constant-time comparison
            // 4. Returns true if match
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (SaltParseException ex)
        {
            // Invalid hash format
            throw new ArgumentException(
                "Invalid password hash format. Expected BCrypt hash starting with $2a$, $2b$, or $2y$",
                nameof(passwordHash),
                ex
            );
        }
    }
}
