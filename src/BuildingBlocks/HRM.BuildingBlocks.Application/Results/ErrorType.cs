namespace HRM.BuildingBlocks.Application.Results;

/// <summary>
/// Defines types of errors that can occur in the application.
/// Each error type maps to an HTTP status code for API responses.
/// 
/// Design Decision:
/// We intentionally include HTTP status codes in the enum values for pragmatic reasons:
/// - HRM is an API-first monolith where HTTP is the primary protocol
/// - Direct mapping reduces boilerplate code in API layer
/// - Improves developer productivity and reduces potential mapping bugs
/// - Industry practice: Many frameworks (ASP.NET ProblemDetails) use similar approach
/// 
/// If HRM needs to support non-HTTP protocols (gRPC, message queues), 
/// the mapping layer can be refactored at that time.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// No error - used only for Error.None in successful results.
    /// This is not a real HTTP status code, just a marker value.
    /// Must be 0 to support default(ErrorType) checks.
    /// </summary>
    None = 0,

    /// <summary>
    /// Validation error (HTTP 400 Bad Request).
    /// Indicates invalid input data or business rule violations at the input level.
    /// 
    /// Examples:
    /// - Invalid email format
    /// - Required field missing
    /// - Value out of acceptable range
    /// - FluentValidation failures
    /// </summary>
    Validation = 400,

    /// <summary>
    /// Unauthorized error (HTTP 401 Unauthorized).
    /// Indicates authentication failure - user identity cannot be verified.
    /// 
    /// Examples:
    /// - Invalid credentials (wrong username/password)
    /// - Expired JWT token
    /// - Malformed JWT token
    /// - Missing authentication header
    /// </summary>
    Unauthorized = 401,

    /// <summary>
    /// Forbidden error (HTTP 403 Forbidden).
    /// Indicates authorization failure - user is authenticated but lacks permission.
    /// 
    /// Examples:
    /// - User trying to access resource outside their scope level
    /// - Missing required role for operation
    /// - Attempting to modify another user's data
    /// </summary>
    Forbidden = 403,

    /// <summary>
    /// Not found error (HTTP 404 Not Found).
    /// Indicates requested resource does not exist.
    /// 
    /// Examples:
    /// - Operator ID not found in database
    /// - Employee ID does not exist
    /// - Department ID invalid
    /// </summary>
    NotFound = 404,

    /// <summary>
    /// Conflict error (HTTP 409 Conflict).
    /// Indicates operation cannot complete due to conflict with current state.
    /// 
    /// Examples:
    /// - Duplicate username (unique constraint violation)
    /// - Duplicate email address
    /// - Concurrent modification conflict (optimistic locking)
    /// - Business rule preventing state change
    /// </summary>
    Conflict = 409,

    /// <summary>
    /// Internal server error (HTTP 500 Internal Server Error).
    /// Indicates unexpected system failure or unhandled exception.
    /// 
    /// Examples:
    /// - Database connection failure
    /// - Unexpected null reference
    /// - Third-party service unavailable
    /// - Unhandled business logic exception
    /// 
    /// Note: This should be rare if proper error handling is implemented.
    /// </summary>
    Failure = 500
}
