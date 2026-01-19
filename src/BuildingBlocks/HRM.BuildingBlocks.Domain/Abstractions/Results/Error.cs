namespace HRM.BuildingBlocks.Domain.Abstractions.Results;

/// <summary>
/// Represents an error that occurred during application execution.
/// Immutable record for functional error handling without exceptions.
///
/// Design Principles:
/// - Immutable: Thread-safe, prevents accidental modifications
/// - Factory methods: Enforce correct ErrorType for each error category
/// - Descriptive: Code + Message + Optional Details provide full context
/// - Serializable: Can be sent over HTTP/gRPC/message queues
///
/// Error Code Convention:
/// Format: "{Entity}.{ErrorName}"
/// Examples:
/// - "Operator.NotFound" - Operator entity not found
/// - "Operator.DuplicateUsername" - Username already exists
/// - "User.DuplicateEmail" - Email already exists
/// - "Validation.Failed" - Multiple validation errors
/// - "Authentication.InvalidCredentials" - Login failed
///
/// Usage Examples:
/// <code>
/// // Not found error
/// return Result.Failure(
///     Error.NotFound("Operator.NotFound", $"Operator with ID {id} not found")
/// );
///
/// // Conflict error
/// return Result.Failure(
///     Error.Conflict("Operator.DuplicateUsername", $"Username '{username}' already exists")
/// );
///
/// // Validation error with details
/// var errors = new Dictionary&lt;string, string[]&gt;
/// {
///     ["Username"] = new[] { "Required", "Must be at least 3 characters" },
///     ["Email"] = new[] { "Invalid email format" }
/// };
/// return Result.Failure(
///     Error.Validation("Validation.Failed", "One or more validation errors occurred", errors)
/// );
/// </code>
/// </summary>
public sealed record Error
{
    /// <summary>
    /// Unique error code identifying the error type.
    /// Should follow the convention: "{Entity}.{ErrorName}"
    /// Used for error categorization and client-side error handling.
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// Should be clear, actionable, and appropriate for display to end users.
    /// Avoid technical jargon when possible.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Type of error, maps to HTTP status code.
    /// Determines how the error should be handled and returned to clients.
    /// </summary>
    public ErrorType Type { get; init; }

    /// <summary>
    /// Additional error details (optional).
    /// Primarily used for validation errors with multiple field-level errors.
    ///
    /// Structure:
    /// - Key: Field name (e.g., "Username", "Email")
    /// - Value: Array of error messages for that field
    ///
    /// Example:
    /// {
    ///   "Username": ["Required", "Must be at least 3 characters", "Cannot contain spaces"],
    ///   "Email": ["Invalid email format"],
    ///   "Password": ["Must contain at least one uppercase letter"]
    /// }
    /// </summary>
    public IDictionary<string, string[]>? Details { get; init; }

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// Ensures errors are created with appropriate ErrorType.
    /// </summary>
    private Error(string code, string message, ErrorType type, IDictionary<string, string[]>? details = null)
    {
        Code = code;
        Message = message;
        Type = type;
        Details = details;
    }

    /// <summary>
    /// Create a validation error (HTTP 400 Bad Request).
    /// Used when input validation fails or business rules are violated at input level.
    /// </summary>
    /// <param name="code">Error code (e.g., "Validation.Failed")</param>
    /// <param name="message">Human-readable error message</param>
    /// <param name="details">Optional field-level validation errors</param>
    public static Error Validation(
        string code,
        string message,
        IDictionary<string, string[]>? details = null)
        => new(code, message, ErrorType.Validation, details);

    /// <summary>
    /// Create an unauthorized error (HTTP 401 Unauthorized).
    /// Used when authentication fails - user identity cannot be verified.
    /// </summary>
    /// <param name="code">Error code (e.g., "Authentication.InvalidCredentials")</param>
    /// <param name="message">Human-readable error message</param>
    public static Error Unauthorized(string code, string message)
        => new(code, message, ErrorType.Unauthorized);

    /// <summary>
    /// Create a forbidden error (HTTP 403 Forbidden).
    /// Used when user is authenticated but lacks permission for the operation.
    /// </summary>
    /// <param name="code">Error code (e.g., "Authorization.InsufficientPermissions")</param>
    /// <param name="message">Human-readable error message</param>
    public static Error Forbidden(string code, string message)
        => new(code, message, ErrorType.Forbidden);

    /// <summary>
    /// Create a not found error (HTTP 404 Not Found).
    /// Used when requested resource does not exist.
    /// </summary>
    /// <param name="code">Error code (e.g., "Operator.NotFound")</param>
    /// <param name="message">Human-readable error message</param>
    public static Error NotFound(string code, string message)
        => new(code, message, ErrorType.NotFound);

    /// <summary>
    /// Create a conflict error (HTTP 409 Conflict).
    /// Used when operation conflicts with current state (e.g., duplicate key).
    /// </summary>
    /// <param name="code">Error code (e.g., "Operator.DuplicateUsername")</param>
    /// <param name="message">Human-readable error message</param>
    public static Error Conflict(string code, string message)
        => new(code, message, ErrorType.Conflict);

    /// <summary>
    /// Create a failure error (HTTP 500 Internal Server Error).
    /// Used for unexpected system failures and unhandled exceptions.
    /// </summary>
    /// <param name="code">Error code (e.g., "System.UnexpectedError")</param>
    /// <param name="message">Human-readable error message</param>
    public static Error Failure(string code, string message)
        => new(code, message, ErrorType.Failure);

    /// <summary>
    /// Predefined error representing no error (successful operation).
    /// Used internally by Result.Success().
    /// Should never be used directly in application code.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
}
