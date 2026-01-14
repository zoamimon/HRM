namespace HRM.BuildingBlocks.Application.Abstractions.Authentication;

/// <summary>
/// Service for secure password hashing and verification.
/// Uses industry-standard algorithms (BCrypt or Argon2) for password security.
/// 
/// Security Principles:
/// 1. Never store passwords in plaintext
/// 2. Use slow, adaptive hashing algorithms (BCrypt/Argon2)
/// 3. Automatic salt generation per password
/// 4. Configurable work factor to resist brute-force attacks
/// 5. Constant-time comparison to prevent timing attacks
/// 
/// Implementation Details:
/// - BCrypt: Work factor 12 (good balance of security vs performance)
/// - Argon2: Memory-hard algorithm (most secure, recommended for new projects)
/// - Salt: Generated automatically, embedded in hash output
/// - Hash Format: Includes algorithm version, cost, salt, and hash
/// 
/// Algorithm Selection:
/// - BCrypt: Mature, widely tested, good for most applications
/// - Argon2: Modern, memory-hard, winner of Password Hashing Competition
/// 
/// Recommended: Argon2id (hybrid of Argon2i and Argon2d)
/// 
/// Performance Considerations:
/// - Hashing is intentionally slow (prevent brute-force)
/// - BCrypt work factor 12: ~100-200ms per hash
/// - Don't hash passwords in tight loops
/// - Hash once during registration/password change
/// - Verify once during login
/// 
/// Usage in Registration:
/// <code>
/// public class RegisterOperatorCommandHandler
/// {
///     private readonly IPasswordHasher _passwordHasher;
///     
///     public async Task&lt;Result&lt;Guid&gt;&gt; Handle(...)
///     {
///         // Hash password before storing
///         var hashedPassword = _passwordHasher.HashPassword(command.Password);
///         
///         // Create operator with hashed password
///         var @operator = Operator.Register(
///             command.Username,
///             command.Email,
///             hashedPassword // ← Hashed, never plaintext
///         );
///         
///         await _repository.AddAsync(@operator);
///         return Result.Success(@operator.Id);
///     }
/// }
/// </code>
/// 
/// Usage in Login:
/// <code>
/// public class LoginCommandHandler
/// {
///     private readonly IPasswordHasher _passwordHasher;
///     
///     public async Task&lt;Result&lt;LoginResult&gt;&gt; Handle(...)
///     {
///         // Find user by username
///         var @operator = await _repository.GetByUsernameAsync(command.Username);
///         if (@operator is null)
///             return Result.Failure(Error.Unauthorized(...));
///         
///         // Verify provided password against stored hash
///         bool isValid = _passwordHasher.VerifyPassword(
///             command.Password,           // Plaintext from user
///             @operator.GetPasswordHash() // Hashed from database
///         );
///         
///         if (!isValid)
///             return Result.Failure(Error.Unauthorized(...));
///         
///         // Password correct, proceed with login
///         var token = _tokenService.GenerateAccessToken(@operator);
///         return Result.Success(token);
///     }
/// }
/// </code>
/// 
/// Security Best Practices:
/// 
/// 1. Never Log Passwords:
/// <code>
/// // ❌ BAD - Don't do this!
/// _logger.LogInformation("User registered with password: {Password}", command.Password);
/// 
/// // ✅ GOOD
/// _logger.LogInformation("User {Username} registered successfully", command.Username);
/// </code>
/// 
/// 2. Don't Return Password Hashes:
/// <code>
/// // ❌ BAD - Don't include in DTOs
/// public class OperatorDto
/// {
///     public string PasswordHash { get; set; } // Never expose this!
/// }
/// 
/// // ✅ GOOD
/// public class OperatorDto
/// {
///     public Guid Id { get; set; }
///     public string Username { get; set; }
///     // No password-related fields
/// }
/// </code>
/// 
/// 3. Use HTTPS:
/// - Passwords transmitted over encrypted connection
/// - Prevent man-in-the-middle attacks
/// - TLS 1.2+ required
/// 
/// 4. Password Policy:
/// - Enforce strong passwords (min length, complexity)
/// - Check against common password lists
/// - Implement in validation layer (FluentValidation)
/// 
/// Example Password Validation:
/// <code>
/// public class RegisterOperatorCommandValidator : AbstractValidator&lt;RegisterOperatorCommand&gt;
/// {
///     public RegisterOperatorCommandValidator()
///     {
///         RuleFor(x => x.Password)
///             .MinimumLength(8)
///             .Matches("[A-Z]").WithMessage("Must contain uppercase letter")
///             .Matches("[a-z]").WithMessage("Must contain lowercase letter")
///             .Matches("[0-9]").WithMessage("Must contain digit")
///             .Matches("[^a-zA-Z0-9]").WithMessage("Must contain special character");
///     }
/// }
/// </code>
/// 
/// Timing Attack Prevention:
/// - Hash verification uses constant-time comparison
/// - Prevents attackers from determining correct characters via timing
/// - Both implementations (BCrypt/Argon2) handle this automatically
/// 
/// Password Change Flow:
/// <code>
/// public class ChangePasswordCommandHandler
/// {
///     public async Task&lt;Result&gt; Handle(...)
///     {
///         // 1. Load operator
///         var @operator = await _repository.GetByIdAsync(command.OperatorId);
///         
///         // 2. Verify old password
///         bool isValid = _passwordHasher.VerifyPassword(
///             command.OldPassword,
///             @operator.GetPasswordHash()
///         );
///         if (!isValid)
///             return Result.Failure(Error.Unauthorized(...));
///         
///         // 3. Hash new password
///         var newHash = _passwordHasher.HashPassword(command.NewPassword);
///         
///         // 4. Update operator
///         @operator.ChangePassword(newHash);
///         
///         return Result.Success();
///     }
/// }
/// </code>
/// 
/// Thread Safety:
/// - Implementations must be thread-safe
/// - Typically stateless (registered as singleton)
/// - No shared mutable state
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using a secure algorithm (BCrypt or Argon2).
    /// 
    /// Process:
    /// 1. Generate random salt (automatic)
    /// 2. Combine password + salt
    /// 3. Apply hashing algorithm with work factor
    /// 4. Return hash string (includes algorithm, cost, salt, hash)
    /// 
    /// Output Format (BCrypt example):
    /// $2a$12$R9h/cIPz0gi.URNNX3kh2OPST9/PgBkqquzi.Ss7KIUgO2t0jWMUW
    /// ││└┘└───────────────┘└───────────────────────────────┘
    /// ││ │        │                     └─ Hash (31 chars)
    /// ││ │        └─ Salt (22 chars)
    /// ││ └─ Cost (12 = 2^12 rounds = 4096 iterations)
    /// │└─ BCrypt version (2a)
    /// └─ Algorithm identifier ($)
    /// 
    /// Performance:
    /// - BCrypt work 12: ~100-200ms (acceptable for login)
    /// - Argon2: ~200-300ms (more secure, slightly slower)
    /// - Intentionally slow to prevent brute-force attacks
    /// 
    /// Security:
    /// - Salt is unique per password
    /// - Work factor makes brute-force expensive
    /// - Future-proof (can increase work factor as hardware improves)
    /// </summary>
    /// <param name="password">Plaintext password from user input (never store this!)</param>
    /// <returns>
    /// Hashed password string containing algorithm, cost, salt, and hash.
    /// Store this in database, never the plaintext password.
    /// </returns>
    /// <exception cref="ArgumentNullException">If password is null</exception>
    /// <exception cref="ArgumentException">If password is empty</exception>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a stored hash.
    /// Uses constant-time comparison to prevent timing attacks.
    /// 
    /// Process:
    /// 1. Extract algorithm, cost, and salt from hash string
    /// 2. Hash provided password using same algorithm + salt
    /// 3. Compare hashes using constant-time comparison
    /// 4. Return true if match, false otherwise
    /// 
    /// Security:
    /// - Constant-time comparison prevents timing attacks
    /// - Failed verification doesn't reveal which part is wrong
    /// - No information leakage about stored hash
    /// 
    /// Performance:
    /// - Same cost as HashPassword (~100-200ms)
    /// - Acceptable for login verification
    /// - Don't call in loops or tight iterations
    /// 
    /// Common Usage Errors:
    /// 
    /// ❌ WRONG - Comparing hashes directly:
    /// <code>
    /// var userHash = _passwordHasher.HashPassword(providedPassword);
    /// if (userHash == storedHash) // This won't work! Different salts!
    /// </code>
    /// 
    /// ✅ CORRECT - Use VerifyPassword:
    /// <code>
    /// if (_passwordHasher.VerifyPassword(providedPassword, storedHash))
    /// {
    ///     // Password is correct
    /// }
    /// </code>
    /// 
    /// Why Direct Hash Comparison Fails:
    /// - Each hash has unique salt
    /// - Same password → different hashes each time
    /// - Must use verification function provided by algorithm
    /// 
    /// Failed Login Handling:
    /// <code>
    /// bool isValid = _passwordHasher.VerifyPassword(
    ///     command.Password,
    ///     storedHash
    /// );
    /// 
    /// if (!isValid)
    /// {
    ///     // Log failed attempt (for security monitoring)
    ///     _logger.LogWarning(
    ///         "Failed login attempt for username {Username}",
    ///         command.Username
    ///     );
    ///     
    ///     // Return generic error (don't reveal if username exists)
    ///     return Result.Failure(
    ///         Error.Unauthorized(
    ///             "Auth.InvalidCredentials",
    ///             "Invalid username or password"
    ///         )
    ///     );
    /// }
    /// </code>
    /// </summary>
    /// <param name="password">Plaintext password provided by user</param>
    /// <param name="passwordHash">Hashed password from database</param>
    /// <returns>True if password matches hash, false otherwise</returns>
    /// <exception cref="ArgumentNullException">If password or passwordHash is null</exception>
    /// <exception cref="ArgumentException">If passwordHash format is invalid</exception>
    bool VerifyPassword(string password, string passwordHash);
}
