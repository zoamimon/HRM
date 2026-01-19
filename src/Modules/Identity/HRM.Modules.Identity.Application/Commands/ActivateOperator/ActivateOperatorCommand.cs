using HRM.BuildingBlocks.Application.Abstractions.Commands;

namespace HRM.Modules.Identity.Application.Commands.ActivateOperator;

/// <summary>
/// Command to activate a pending operator
/// Admin-only operation
///
/// CQRS Pattern:
/// - Command: Mutates system state (changes operator status)
/// - Returns: Unit (void) on success, Error on failure (Result pattern)
/// - Idempotent: Yes (activating already active operator returns success)
///
/// Security:
/// - Authorization: [Authorize(Policy = "AdminOnly")] on endpoint
/// - Only administrators can activate operators
/// - Self-activation not allowed
///
/// Validation:
/// - OperatorId: Valid GUID, operator must exist
///
/// Business Rules:
/// - Operator status: Pending → Active
/// - Sets ActivatedAtUtc to current UTC time
/// - Domain event raised: OperatorActivatedDomainEvent
/// - Integration event created: OperatorActivatedIntegrationEvent (optional)
/// - If already Active: Return success (idempotent)
/// - If Suspended/Deactivated: Cannot activate (must use different command)
///
/// Flow:
/// 1. Validate command (FluentValidation)
/// 2. Retrieve operator by ID (404 if not found)
/// 3. Call operator.Activate() (domain logic)
/// 4. Save to database (UnitOfWork)
/// 5. Domain event dispatched (creates integration event)
/// 6. Return success
///
/// Usage (API):
/// <code>
/// POST /api/identity/operators/{operatorId}/activate
/// Authorization: Bearer {admin_token}
///
/// Response (200 OK):
/// {
///   "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "username": "john.doe",
///   "email": "john.doe@company.com",
///   "fullName": "John Doe",
///   "status": "Active",
///   "activatedAtUtc": "2024-01-15T10:30:00Z"
/// }
/// </code>
/// </summary>
/// <param name="OperatorId">ID of operator to activate</param>
public sealed record ActivateOperatorCommand(
    Guid OperatorId
) : ICommand;
