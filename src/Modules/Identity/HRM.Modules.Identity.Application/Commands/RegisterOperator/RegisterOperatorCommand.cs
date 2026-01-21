using HRM.BuildingBlocks.Application.Abstractions.Commands;

namespace HRM.Modules.Identity.Application.Commands.RegisterOperator;

/// <summary>
/// Command to register a new operator
/// Admin-only operation (or seed first operator)
///
/// CQRS Pattern:
/// - Command: Mutates system state (creates new operator)
/// - Returns: Operator ID on success, Error on failure (Result pattern)
/// - Idempotent: No (duplicate username/email returns error)
///
/// Security:
/// - Authorization: [Authorize(Policy = "AdminOnly")] on endpoint
/// - First operator: Seeded via database script (admin/Admin@123456)
/// - Subsequent operators: Registered by existing admins
///
/// Validation:
/// - Username: 3-50 chars, alphanumeric with underscores/hyphens, unique
/// - Email: Valid format (RFC 5322), unique
/// - Password: 12+ chars, uppercase, lowercase, digit, special character
/// - FullName: 1-200 chars, required
/// - PhoneNumber: Optional, valid format if provided
///
/// Business Rules:
/// - Operator created in Pending status (must be activated by admin)
/// - Password hashed with BCrypt before storage
/// - Domain event raised: OperatorRegisteredDomainEvent
/// - Integration event created: OperatorRegisteredIntegrationEvent (optional)
///
/// Flow:
/// 1. Validate command (FluentValidation)
/// 2. Check username/email uniqueness
/// 3. Hash password with BCrypt
/// 4. Create Operator aggregate via Operator.Register()
/// 5. Save to database (UnitOfWork)
/// 6. Domain event dispatched (creates integration event)
/// 7. Return operator ID
///
/// Usage (API):
/// <code>
/// POST /api/identity/operators/register
/// Authorization: Bearer {admin_token}
/// {
///   "username": "john.doe",
///   "email": "john.doe@company.com",
///   "password": "StrongPassword123!",
///   "fullName": "John Doe",
///   "phoneNumber": "+1234567890"
/// }
///
/// Response (201 Created):
/// {
///   "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "username": "john.doe",
///   "email": "john.doe@company.com",
///   "fullName": "John Doe",
///   "status": "Pending"
/// }
/// </code>
/// </summary>
/// <param name="Username">Username (3-50 chars, alphanumeric with underscores/hyphens)</param>
/// <param name="Email">Email address (must be valid format and unique)</param>
/// <param name="Password">Password (12+ chars with complexity requirements)</param>
/// <param name="FullName">Full name (1-200 chars)</param>
/// <param name="PhoneNumber">Phone number (optional, E.164 format recommended)</param>
public sealed record RegisterOperatorCommand(
    string Username,
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber = null
) : IModuleCommand<Guid>
{
    /// <summary>
    /// Module name for Unit of Work routing
    /// Commands in Identity module always commit via IdentityDbContext
    /// </summary>
    public string ModuleName => "Identity";
}
