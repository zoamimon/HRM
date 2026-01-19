using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Domain.Errors;

/// <summary>
/// Static error definitions for Operator operations
/// Used with Result pattern for type-safe error handling
///
/// Error Categories:
/// - Conflict: Resource already exists (username, email)
/// - NotFound: Operator not found by ID/username
/// - Validation: Business rule violations (password, email format)
/// - Forbidden: Authorization failures (account locked, not activated)
///
/// Usage in Commands/Queries:
/// <code>
/// if (await _repository.ExistsByUsernameAsync(username))
///     return Result.Failure<Guid>(OperatorErrors.UsernameAlreadyExists(username));
/// </code>
/// </summary>
public static class OperatorErrors
{
    /// <summary>
    /// Username already exists in the system
    /// Conflict (409) - Cannot register with duplicate username
    /// </summary>
    public static Error UsernameAlreadyExists(string username) =>
        Error.Conflict(
            "Operator.UsernameAlreadyExists",
            $"Username '{username}' is already taken. Please choose a different username."
        );

    /// <summary>
    /// Email already exists in the system
    /// Conflict (409) - Cannot register with duplicate email
    /// </summary>
    public static Error EmailAlreadyExists(string email) =>
        Error.Conflict(
            "Operator.EmailAlreadyExists",
            $"Email '{email}' is already registered. Please use a different email address."
        );

    /// <summary>
    /// Operator not found by ID
    /// NotFound (404) - Invalid operator ID in request
    /// </summary>
    public static Error NotFound(Guid id) =>
        Error.NotFound(
            "Operator.NotFound",
            $"Operator with ID '{id}' was not found."
        );

    /// <summary>
    /// Operator not found by username
    /// NotFound (404) - Invalid username in login request
    /// </summary>
    public static Error NotFoundByUsername(string username) =>
        Error.NotFound(
            "Operator.NotFoundByUsername",
            $"Operator with username '{username}' was not found."
        );

    /// <summary>
    /// Password too short
    /// Validation (400) - Password doesn't meet minimum length requirement
    /// </summary>
    public static Error PasswordTooShort(int minLength) =>
        Error.Validation(
            "Operator.PasswordTooShort",
            $"Password must be at least {minLength} characters long."
        );

    /// <summary>
    /// Password complexity requirements not met
    /// Validation (400) - Password must contain uppercase, lowercase, digit, special character
    /// </summary>
    public static readonly Error PasswordComplexityNotMet = Error.Validation(
        "Operator.PasswordComplexityNotMet",
        "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character."
    );

    /// <summary>
    /// Invalid email format
    /// Validation (400) - Email doesn't match RFC 5322 format
    /// </summary>
    public static readonly Error InvalidEmailFormat = Error.Validation(
        "Operator.InvalidEmailFormat",
        "Email address format is invalid. Please provide a valid email address."
    );

    /// <summary>
    /// Username contains invalid characters
    /// Validation (400) - Username must be alphanumeric with underscores/hyphens
    /// </summary>
    public static readonly Error InvalidUsernameFormat = Error.Validation(
        "Operator.InvalidUsernameFormat",
        "Username can only contain letters, numbers, underscores, and hyphens."
    );

    /// <summary>
    /// Account is locked due to failed login attempts
    /// Forbidden (403) - Cannot login until lock expires
    /// </summary>
    public static Error AccountLocked(DateTime lockedUntil) =>
        Error.Forbidden(
            "Operator.AccountLocked",
            $"Account is locked due to too many failed login attempts. Please try again after {lockedUntil:yyyy-MM-dd HH:mm:ss} UTC."
        );

    /// <summary>
    /// Account not activated yet
    /// Forbidden (403) - Cannot login with Pending status
    /// </summary>
    public static readonly Error AccountNotActivated = Error.Forbidden(
        "Operator.AccountNotActivated",
        "Account is not activated yet. Please contact administrator to activate your account."
    );

    /// <summary>
    /// Account is suspended
    /// Forbidden (403) - Cannot login with Suspended status
    /// </summary>
    public static readonly Error AccountSuspended = Error.Forbidden(
        "Operator.AccountSuspended",
        "Account is suspended. Please contact administrator for assistance."
    );

    /// <summary>
    /// Account is deactivated
    /// Forbidden (403) - Cannot login with Deactivated status
    /// </summary>
    public static readonly Error AccountDeactivated = Error.Forbidden(
        "Operator.AccountDeactivated",
        "Account is deactivated. Please contact administrator to reactivate your account."
    );

    /// <summary>
    /// Invalid credentials (wrong password)
    /// Unauthorized (401) - Login failed
    /// </summary>
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Operator.InvalidCredentials",
        "Invalid username or password. Please check your credentials and try again."
    );

    /// <summary>
    /// Operator already activated
    /// Conflict (409) - Cannot activate an already active account
    /// </summary>
    public static Error AlreadyActivated(string username) =>
        Error.Conflict(
            "Operator.AlreadyActivated",
            $"Operator '{username}' is already activated."
        );

    /// <summary>
    /// Two-factor authentication already enabled
    /// Conflict (409) - Cannot enable 2FA twice
    /// </summary>
    public static readonly Error TwoFactorAlreadyEnabled = Error.Conflict(
        "Operator.TwoFactorAlreadyEnabled",
        "Two-factor authentication is already enabled for this account."
    );

    /// <summary>
    /// Two-factor authentication not enabled
    /// Conflict (409) - Cannot disable 2FA if not enabled
    /// </summary>
    public static readonly Error TwoFactorNotEnabled = Error.Conflict(
        "Operator.TwoFactorNotEnabled",
        "Two-factor authentication is not enabled for this account."
    );

    /// <summary>
    /// Invalid two-factor code
    /// Unauthorized (401) - 2FA verification failed
    /// </summary>
    public static readonly Error InvalidTwoFactorCode = Error.Unauthorized(
        "Operator.InvalidTwoFactorCode",
        "Invalid two-factor authentication code. Please try again."
    );
}
