namespace HRM.BuildingBlocks.Domain.Abstractions.Results;

/// <summary>
/// Base class for all domain errors.
/// PURE DOMAIN - No HTTP, no transport concerns, no infrastructure dependencies.
///
/// Design Principles:
/// - Transport-agnostic: No HTTP status codes, no protocol knowledge
/// - Reusable: Can be used in CLI, background jobs, gRPC, message consumers
/// - Testable: Domain tests don't need HTTP concepts
/// - DDD-compliant: Domain errors express business concepts, not technical concerns
///
/// Error Code Convention:
/// Format: "{Entity}.{ErrorName}"
/// Examples:
/// - "Operator.NotFound"
/// - "Operator.UsernameAlreadyExists"
/// - "Employee.InvalidHireDate"
///
/// HTTP Mapping:
/// Mapping to HTTP status codes happens in API layer via ResultExtensions.
/// This keeps Domain pure and allows for multiple transport types.
///
/// Usage in Domain:
/// <code>
/// public static class OperatorErrors
/// {
///     public static NotFoundError NotFound(Guid id) =>
///         new("Operator.NotFound", $"Operator with ID '{id}' not found");
///
///     public static ConflictError UsernameAlreadyExists(string username) =>
///         new("Operator.UsernameAlreadyExists", $"Username '{username}' already exists");
/// }
/// </code>
///
/// Usage in Application:
/// <code>
/// if (!await _repository.ExistsByIdAsync(id))
///     return Result.Failure(OperatorErrors.NotFound(id));
/// </code>
/// </summary>
public abstract record DomainError
{
    /// <summary>
    /// Unique error code identifying the error type.
    /// Format: "{Entity}.{ErrorName}"
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable error message.
    /// Should be clear and appropriate for display to end users.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Protected constructor to enforce factory pattern usage.
    /// Derived types should be created via static factory methods in error classes.
    /// </summary>
    protected DomainError(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Error code cannot be null or empty", nameof(code));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message cannot be null or empty", nameof(message));

        Code = code;
        Message = message;
    }
}

/// <summary>
/// Requested resource does not exist.
/// Typically maps to HTTP 404 Not Found in API layer.
///
/// Use Cases:
/// - Entity not found by ID
/// - Resource doesn't exist in database
/// - Reference to non-existent entity
///
/// Examples:
/// - Operator not found
/// - Employee not found
/// - Department not found
/// </summary>
public sealed record NotFoundError(string Code, string Message)
    : DomainError(Code, Message);

/// <summary>
/// Operation conflicts with current system state.
/// Typically maps to HTTP 409 Conflict in API layer.
///
/// Use Cases:
/// - Duplicate unique key (username, email)
/// - Concurrent modification conflict
/// - Business rule preventing state change
/// - Resource already exists
///
/// Examples:
/// - Username already taken
/// - Email already registered
/// - Cannot delete entity with dependencies
/// - Optimistic concurrency conflict
/// </summary>
public sealed record ConflictError(string Code, string Message)
    : DomainError(Code, Message);

/// <summary>
/// Input validation failed or business rule violated.
/// Typically maps to HTTP 400 Bad Request in API layer.
///
/// Use Cases:
/// - Invalid input format
/// - Required field missing
/// - Value out of acceptable range
/// - Business rule violation at input level
///
/// Details Property:
/// Dictionary of field-level validation errors for detailed feedback.
/// Key: Field name, Value: Array of error messages for that field
///
/// Examples:
/// - Password too short
/// - Invalid email format
/// - Age must be between 18 and 65
/// - Start date must be before end date
/// </summary>
public sealed record ValidationError : DomainError
{
    /// <summary>
    /// Field-level validation errors.
    /// Key: Field name, Value: Array of error messages
    /// Null if no field-level details (single validation error)
    /// </summary>
    public IDictionary<string, string[]>? Details { get; }

    public ValidationError(string code, string message, IDictionary<string, string[]>? details = null)
        : base(code, message)
    {
        Details = details;
    }
}

/// <summary>
/// User authentication failed.
/// Typically maps to HTTP 401 Unauthorized in API layer.
///
/// Use Cases:
/// - Invalid credentials (wrong username/password)
/// - Expired authentication token
/// - Missing authentication token
/// - Malformed authentication token
///
/// Examples:
/// - Login failed - invalid credentials
/// - JWT token expired
/// - No authentication header provided
/// </summary>
public sealed record UnauthorizedError(string Code, string Message)
    : DomainError(Code, Message);

/// <summary>
/// User lacks permission for requested operation.
/// Typically maps to HTTP 403 Forbidden in API layer.
///
/// Use Cases:
/// - User authenticated but lacks required role
/// - Attempting to access resource outside user's scope
/// - Operation not allowed for user's permission level
/// - Account locked or suspended
///
/// Examples:
/// - Insufficient permissions
/// - Account locked due to failed login attempts
/// - Cannot access data outside user's department
/// - Operation requires admin role
/// </summary>
public sealed record ForbiddenError(string Code, string Message)
    : DomainError(Code, Message);

/// <summary>
/// Unexpected system failure or unhandled exception.
/// Typically maps to HTTP 500 Internal Server Error in API layer.
///
/// Use Cases:
/// - Database connection failure
/// - External service unavailable
/// - Unexpected null reference
/// - Unhandled business logic exception
///
/// Note: Should be rare if proper error handling is implemented.
/// Most errors should be one of the specific types above.
///
/// Examples:
/// - Database timeout
/// - Third-party API failure
/// - Unexpected system state
/// </summary>
public sealed record FailureError(string Code, string Message)
    : DomainError(Code, Message);
