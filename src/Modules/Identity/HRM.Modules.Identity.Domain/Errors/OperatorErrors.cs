using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Domain.Errors;

/// <summary>
/// Static error definitions for Operator operations.
/// Uses pure DomainError types - no HTTP concerns.
///
/// Error Categories:
/// - ConflictError: Resource already exists (username, email)
/// - NotFoundError: Operator not found by ID/username
/// - ValidationError: Business rule violations (password, email format)
/// - ForbiddenError: Authorization failures (account locked, not activated)
/// - UnauthorizedError: Authentication failures (invalid credentials)
///
/// HTTP Mapping:
/// Mapping to HTTP status codes happens in API layer.
/// This keeps Domain pure and transport-agnostic.
///
/// Usage in Application Layer:
/// <code>
/// if (await _repository.ExistsByUsernameAsync(username))
///     return Result.Failure<Guid>(OperatorErrors.UsernameAlreadyExists(username));
/// </code>
/// </summary>
public static class OperatorErrors
{
    /// <summary>
    /// Username already exists in the system.
    /// Maps to HTTP 409 Conflict in API layer.
    /// </summary>
    public static ConflictError UsernameAlreadyExists(string username) =>
        new("Operator.UsernameAlreadyExists",
            $"Username '{username}' is already taken. Please choose a different username.");

    /// <summary>
    /// Email already exists in the system.
    /// Maps to HTTP 409 Conflict in API layer.
    /// </summary>
    public static ConflictError EmailAlreadyExists(string email) =>
        new("Operator.EmailAlreadyExists",
            $"Email '{email}' is already registered. Please use a different email address.");

    /// <summary>
    /// Operator not found by ID.
    /// Maps to HTTP 404 Not Found in API layer.
    /// </summary>
    public static NotFoundError NotFound(Guid id) =>
        new("Operator.NotFound",
            $"Operator with ID '{id}' was not found.");

    /// <summary>
    /// Operator not found by username.
    /// Maps to HTTP 404 Not Found in API layer.
    /// </summary>
    public static NotFoundError NotFoundByUsername(string username) =>
        new("Operator.NotFoundByUsername",
            $"Operator with username '{username}' was not found.");

    /// <summary>
    /// Password too short.
    /// Maps to HTTP 400 Bad Request in API layer.
    /// </summary>
    public static ValidationError PasswordTooShort(int minLength) =>
        new("Operator.PasswordTooShort",
            $"Password must be at least {minLength} characters long.");

    /// <summary>
    /// Password complexity requirements not met.
    /// Maps to HTTP 400 Bad Request in API layer.
    /// </summary>
    public static readonly ValidationError PasswordComplexityNotMet =
        new("Operator.PasswordComplexityNotMet",
            "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.");

    /// <summary>
    /// Invalid email format.
    /// Maps to HTTP 400 Bad Request in API layer.
    /// </summary>
    public static readonly ValidationError InvalidEmailFormat =
        new("Operator.InvalidEmailFormat",
            "Email address format is invalid. Please provide a valid email address.");

    /// <summary>
    /// Username contains invalid characters.
    /// Maps to HTTP 400 Bad Request in API layer.
    /// </summary>
    public static readonly ValidationError InvalidUsernameFormat =
        new("Operator.InvalidUsernameFormat",
            "Username can only contain letters, numbers, underscores, and hyphens.");

    /// <summary>
    /// Account is locked due to failed login attempts.
    /// Maps to HTTP 403 Forbidden in API layer.
    /// </summary>
    public static ForbiddenError AccountLocked(DateTime lockedUntil) =>
        new("Operator.AccountLocked",
            $"Account is locked due to too many failed login attempts. Please try again after {lockedUntil:yyyy-MM-dd HH:mm:ss} UTC.");

    /// <summary>
    /// Account not activated yet.
    /// Maps to HTTP 403 Forbidden in API layer.
    /// </summary>
    public static readonly ForbiddenError AccountNotActivated =
        new("Operator.AccountNotActivated",
            "Account is not activated yet. Please contact administrator to activate your account.");

    /// <summary>
    /// Account is suspended.
    /// Maps to HTTP 403 Forbidden in API layer.
    /// </summary>
    public static readonly ForbiddenError AccountSuspended =
        new("Operator.AccountSuspended",
            "Account is suspended. Please contact administrator for assistance.");

    /// <summary>
    /// Account is deactivated.
    /// Maps to HTTP 403 Forbidden in API layer.
    /// </summary>
    public static readonly ForbiddenError AccountDeactivated =
        new("Operator.AccountDeactivated",
            "Account is deactivated. Please contact administrator to reactivate your account.");

    /// <summary>
    /// Invalid credentials (wrong password).
    /// Maps to HTTP 401 Unauthorized in API layer.
    /// </summary>
    public static readonly UnauthorizedError InvalidCredentials =
        new("Operator.InvalidCredentials",
            "Invalid username or password. Please check your credentials and try again.");

    /// <summary>
    /// Operator already activated.
    /// Maps to HTTP 409 Conflict in API layer.
    /// </summary>
    public static ConflictError AlreadyActivated(string username) =>
        new("Operator.AlreadyActivated",
            $"Operator '{username}' is already activated.");

    /// <summary>
    /// Two-factor authentication already enabled.
    /// Maps to HTTP 409 Conflict in API layer.
    /// </summary>
    public static readonly ConflictError TwoFactorAlreadyEnabled =
        new("Operator.TwoFactorAlreadyEnabled",
            "Two-factor authentication is already enabled for this account.");

    /// <summary>
    /// Two-factor authentication not enabled.
    /// Maps to HTTP 409 Conflict in API layer.
    /// </summary>
    public static readonly ConflictError TwoFactorNotEnabled =
        new("Operator.TwoFactorNotEnabled",
            "Two-factor authentication is not enabled for this account.");

    /// <summary>
    /// Invalid two-factor code.
    /// Maps to HTTP 401 Unauthorized in API layer.
    /// </summary>
    public static readonly UnauthorizedError InvalidTwoFactorCode =
        new("Operator.InvalidTwoFactorCode",
            "Invalid two-factor authentication code. Please try again.");
}
