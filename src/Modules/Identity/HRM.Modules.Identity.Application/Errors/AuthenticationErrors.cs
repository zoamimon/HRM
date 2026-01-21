using HRM.BuildingBlocks.Domain.Abstractions.Results;

namespace HRM.Modules.Identity.Application.Errors;

/// <summary>
/// Static factory class for authentication-related errors
///
/// Design Pattern: Static Factory
/// - Centralizes error creation for authentication domain
/// - Provides consistent error codes and messages
/// - Easy to discover via IntelliSense
/// - Type-safe error creation
///
/// Error Code Convention:
/// Format: "Authentication.{ErrorName}"
///
/// Usage in Handlers:
/// <code>
/// if (invalidCredentials)
///     return Result.Failure(AuthenticationErrors.InvalidCredentials());
///
/// if (accountLocked)
///     return Result.Failure(AuthenticationErrors.AccountLockedOut(unlockTime));
/// </code>
///
/// HTTP Status Code Mapping (handled in API layer):
/// - InvalidCredentials → 401 Unauthorized
/// - AccountLockedOut → 403 Forbidden
/// - AccountSuspended → 403 Forbidden
/// - InvalidRefreshToken → 401 Unauthorized
/// - RefreshTokenExpired → 401 Unauthorized
/// </summary>
public static class AuthenticationErrors
{
    /// <summary>
    /// Login failed due to invalid username/email or password
    ///
    /// Security Note:
    /// - Generic message prevents username enumeration
    /// - Don't reveal whether username or password was wrong
    /// - Same error for non-existent user and wrong password
    ///
    /// Use Cases:
    /// - User enters wrong password
    /// - User enters non-existent username
    /// - Prevents attackers from discovering valid usernames
    /// </summary>
    public static UnauthorizedError InvalidCredentials() =>
        new(
            "Authentication.InvalidCredentials",
            "The username/email or password is incorrect. Please try again."
        );

    /// <summary>
    /// Account is temporarily locked due to failed login attempts
    ///
    /// Security Feature:
    /// - Prevents brute force attacks
    /// - Temporary lockout (typically 15-30 minutes)
    /// - User can wait or contact admin for unlock
    ///
    /// Use Cases:
    /// - 5+ failed login attempts
    /// - Account locked until specific time
    /// - User should wait or reset password
    /// </summary>
    public static ForbiddenError AccountLockedOut(DateTime? unlockTime = null)
    {
        var message = unlockTime.HasValue
            ? $"Your account has been temporarily locked due to multiple failed login attempts. " +
              $"Please try again after {unlockTime.Value:yyyy-MM-dd HH:mm:ss} UTC."
            : "Your account has been temporarily locked due to multiple failed login attempts. " +
              "Please contact support for assistance.";

        return new ForbiddenError(
            "Authentication.AccountLockedOut",
            message
        );
    }

    /// <summary>
    /// Account is suspended by administrator
    ///
    /// Administrative Action:
    /// - Permanent suspension until admin reactivates
    /// - Typically due to policy violation
    /// - User must contact admin to resolve
    ///
    /// Use Cases:
    /// - Operator status = Suspended
    /// - Admin manually suspended account
    /// - Policy violation or security issue
    /// </summary>
    public static ForbiddenError AccountSuspended() =>
        new(
            "Authentication.AccountSuspended",
            "Your account has been suspended. Please contact your administrator."
        );

    /// <summary>
    /// Account is deactivated (not active)
    ///
    /// Use Cases:
    /// - Operator status = Deactivated or Pending
    /// - Account not yet activated after registration
    /// - Former employee account disabled
    /// </summary>
    public static ForbiddenError AccountNotActive() =>
        new(
            "Authentication.AccountNotActive",
            "Your account is not active. Please contact your administrator."
        );

    /// <summary>
    /// Refresh token is invalid, expired, or revoked
    ///
    /// Security Note:
    /// - Generic message prevents token enumeration
    /// - Don't reveal specific reason (expired vs revoked)
    /// - User must re-login with credentials
    ///
    /// Use Cases:
    /// - Token not found in database
    /// - Token already revoked (logout)
    /// - Token expired (past ExpiresAt)
    /// - Token format invalid
    /// </summary>
    public static UnauthorizedError InvalidRefreshToken() =>
        new(
            "Authentication.InvalidRefreshToken",
            "The refresh token is invalid or has expired. Please log in again."
        );

    /// <summary>
    /// Refresh token has expired (specific error for expiration)
    ///
    /// Use Cases:
    /// - Token ExpiresAt has passed
    /// - User hasn't used app for 7+ days (normal)
    /// - User hasn't used app for 30+ days (Remember Me)
    /// </summary>
    public static UnauthorizedError RefreshTokenExpired() =>
        new(
            "Authentication.RefreshTokenExpired",
            "Your session has expired. Please log in again."
        );

    /// <summary>
    /// Refresh token has been revoked (specific error for revocation)
    ///
    /// Use Cases:
    /// - User logged out from this device
    /// - Admin revoked all user sessions
    /// - Password changed (security measure)
    /// - Token rotation occurred
    /// </summary>
    public static UnauthorizedError RefreshTokenRevoked() =>
        new(
            "Authentication.RefreshTokenRevoked",
            "Your session has been terminated. Please log in again."
        );

    /// <summary>
    /// Two-factor authentication code is invalid
    ///
    /// Use Cases:
    /// - Wrong TOTP code entered
    /// - Code expired (30-second window)
    /// - Code already used
    /// </summary>
    public static UnauthorizedError InvalidTwoFactorCode() =>
        new(
            "Authentication.InvalidTwoFactorCode",
            "The two-factor authentication code is invalid or has expired."
        );

    /// <summary>
    /// Two-factor authentication is required but not provided
    ///
    /// Use Cases:
    /// - User has 2FA enabled
    /// - Initial login succeeded but 2FA pending
    /// - Must provide TOTP code to complete login
    /// </summary>
    public static UnauthorizedError TwoFactorRequired() =>
        new(
            "Authentication.TwoFactorRequired",
            "Two-factor authentication is required to complete login."
        );
}
