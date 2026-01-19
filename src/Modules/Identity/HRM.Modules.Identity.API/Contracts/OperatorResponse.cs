namespace HRM.Modules.Identity.API.Contracts;

/// <summary>
/// Response DTO for operator operations
/// Maps from Operator entity
///
/// Used By:
/// - POST /api/identity/operators/register (201 Created)
/// - POST /api/identity/operators/{id}/activate (200 OK)
/// - GET /api/identity/operators/{id} (200 OK) - future
/// - GET /api/identity/operators (200 OK) - future
///
/// Security:
/// - NEVER expose PasswordHash or TwoFactorSecret
/// - Only include public information
/// - Sensitive fields filtered in mapping
///
/// Status Values:
/// - "Pending": Registered but not activated
/// - "Active": Can login to system
/// - "Suspended": Temporarily disabled
/// - "Deactivated": Permanently disabled
///
/// Example Response:
/// <code>
/// {
///   "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "username": "john.doe",
///   "email": "john.doe@company.com",
///   "fullName": "John Doe",
///   "phoneNumber": "+1234567890",
///   "status": "Active",
///   "isTwoFactorEnabled": false,
///   "activatedAtUtc": "2024-01-15T10:30:00Z",
///   "lastLoginAtUtc": "2024-01-16T08:15:00Z",
///   "createdAtUtc": "2024-01-15T10:00:00Z",
///   "modifiedAtUtc": "2024-01-15T10:30:00Z"
/// }
/// </code>
/// </summary>
/// <param name="Id">Operator ID</param>
/// <param name="Username">Username</param>
/// <param name="Email">Email address</param>
/// <param name="FullName">Full name</param>
/// <param name="PhoneNumber">Phone number (optional)</param>
/// <param name="Status">Account status (Pending, Active, Suspended, Deactivated)</param>
/// <param name="IsTwoFactorEnabled">Whether 2FA is enabled</param>
/// <param name="ActivatedAtUtc">Timestamp when activated (null if not activated)</param>
/// <param name="LastLoginAtUtc">Timestamp of last login (null if never logged in)</param>
/// <param name="CreatedAtUtc">Timestamp when created</param>
/// <param name="ModifiedAtUtc">Timestamp when last modified (null if never modified)</param>
public sealed record OperatorResponse(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? PhoneNumber,
    string Status,
    bool IsTwoFactorEnabled,
    DateTime? ActivatedAtUtc,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc
);
