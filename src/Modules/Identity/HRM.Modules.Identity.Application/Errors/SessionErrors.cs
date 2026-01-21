using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Application.Errors;

/// <summary>
/// Static factory class for session management errors
///
/// Design Pattern: Static Factory
/// - Centralizes error creation for session management
/// - Provides consistent error codes and messages
/// - Easy to discover via IntelliSense
/// - Type-safe error creation
///
/// Error Code Convention:
/// Format: "Session.{ErrorName}"
///
/// Usage in Handlers:
/// <code>
/// if (sessionNotFound)
///     return Result.Failure(SessionErrors.NotFound());
///
/// if (noActiveSession)
///     return Result.Failure(SessionErrors.NoActiveSession());
/// </code>
///
/// HTTP Status Code Mapping (handled in API layer):
/// - NotFound → 404 Not Found
/// - NoActiveSession → 400 Bad Request
/// - SessionAlreadyRevoked → 400 Bad Request
/// - UnauthorizedAccess → 403 Forbidden
/// </summary>
public static class SessionErrors
{
    /// <summary>
    /// Session (refresh token) not found in database
    ///
    /// Use Cases:
    /// - Invalid session ID provided
    /// - Session already deleted (cleanup job)
    /// - User trying to access another user's session
    /// </summary>
    public static NotFoundError NotFound() =>
        new(
            "Session.NotFound",
            "The requested session was not found."
        );

    /// <summary>
    /// Session not found or doesn't belong to current user
    ///
    /// Security:
    /// - Prevents users from accessing others' sessions
    /// - Same error for not found and unauthorized
    /// - Prevents session ID enumeration
    ///
    /// Use Cases:
    /// - User tries to revoke another user's session
    /// - Session ID doesn't exist
    /// - Session belongs to different operator
    /// </summary>
    public static NotFoundError NotFoundOrUnauthorized() =>
        new(
            "Session.NotFoundOrUnauthorized",
            "The session was not found or you don't have permission to access it."
        );

    /// <summary>
    /// No active session found for current user
    ///
    /// Use Cases:
    /// - User tries to revoke all sessions but has none
    /// - GetActiveSessions returns empty
    /// - User already logged out from all devices
    /// </summary>
    public static ValidationError NoActiveSession() =>
        new(
            "Session.NoActiveSession",
            "No active session found. Please log in first."
        );

    /// <summary>
    /// Cannot revoke current session when using "revoke all except current"
    ///
    /// Use Cases:
    /// - No refresh token in cookie/header
    /// - Current token already revoked
    /// - Invalid current token
    /// </summary>
    public static ValidationError CannotIdentifyCurrentSession() =>
        new(
            "Session.CannotIdentifyCurrentSession",
            "Cannot identify your current session. Please log in again."
        );

    /// <summary>
    /// Session is already revoked (idempotent operation)
    ///
    /// Note:
    /// - This is actually success case (idempotency)
    /// - Can return Result.Success() instead
    /// - Included for completeness
    ///
    /// Use Cases:
    /// - User clicks logout twice
    /// - Session already revoked by another action
    /// - Cleanup job already processed this session
    /// </summary>
    public static ValidationError AlreadyRevoked() =>
        new(
            "Session.AlreadyRevoked",
            "This session has already been terminated."
        );

    /// <summary>
    /// User trying to access or revoke another user's session
    ///
    /// Security Error:
    /// - Serious security violation attempt
    /// - Should be logged for audit
    /// - May indicate compromised account
    ///
    /// Use Cases:
    /// - User tries to revoke session with different OperatorId
    /// - Attempting horizontal privilege escalation
    /// - Token manipulation attempt
    /// </summary>
    public static ForbiddenError UnauthorizedAccess() =>
        new(
            "Session.UnauthorizedAccess",
            "You do not have permission to access this session."
        );

    /// <summary>
    /// Session has expired (past ExpiresAt date)
    ///
    /// Use Cases:
    /// - Session ExpiresAt has passed
    /// - User trying to use old session
    /// - Should log in again
    /// </summary>
    public static ValidationError Expired() =>
        new(
            "Session.Expired",
            "This session has expired. Please log in again."
        );

    /// <summary>
    /// Too many active sessions for user (rate limiting)
    ///
    /// Optional Feature:
    /// - Prevent session flooding
    /// - Limit concurrent devices
    /// - Security measure
    ///
    /// Use Cases:
    /// - User has 10+ active sessions
    /// - Potential account compromise
    /// - Enforce device limits
    /// </summary>
    public static ValidationError TooManySessions(int maxSessions) =>
        new(
            "Session.TooManySessions",
            $"You have reached the maximum number of active sessions ({maxSessions}). " +
            "Please log out from other devices first."
        );
}
