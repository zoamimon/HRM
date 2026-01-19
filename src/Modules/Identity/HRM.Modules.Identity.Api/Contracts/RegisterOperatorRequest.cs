namespace HRM.Modules.Identity.Api.Contracts;

/// <summary>
/// Request DTO for registering a new operator
/// Maps to RegisterOperatorCommand
///
/// HTTP Endpoint:
/// POST /api/identity/operators/register
/// Authorization: Bearer {admin_token}
/// Content-Type: application/json
///
/// Validation:
/// - Handled by FluentValidation (RegisterOperatorCommandValidator)
/// - Returns 400 Bad Request with validation errors if invalid
///
/// Example Request:
/// <code>
/// {
///   "username": "john.doe",
///   "email": "john.doe@company.com",
///   "password": "StrongPassword123!",
///   "fullName": "John Doe",
///   "phoneNumber": "+1234567890"
/// }
/// </code>
///
/// Example Response (201 Created):
/// <code>
/// {
///   "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "username": "john.doe",
///   "email": "john.doe@company.com",
///   "fullName": "John Doe",
///   "phoneNumber": "+1234567890",
///   "status": "Pending",
///   "createdAtUtc": "2024-01-15T10:30:00Z"
/// }
/// </code>
///
/// Example Error Response (409 Conflict):
/// <code>
/// {
///   "code": "Operator.UsernameAlreadyExists",
///   "message": "Username 'john.doe' is already taken. Please choose a different username."
/// }
/// </code>
/// </summary>
/// <param name="Username">Username (3-50 chars, alphanumeric with underscores/hyphens)</param>
/// <param name="Email">Email address (valid format, unique)</param>
/// <param name="Password">Password (12+ chars with complexity requirements)</param>
/// <param name="FullName">Full name (1-200 chars)</param>
/// <param name="PhoneNumber">Phone number (optional, E.164 format recommended)</param>
public sealed record RegisterOperatorRequest(
    string Username,
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber = null
);
