namespace HRM.BuildingBlocks.Domain.Events;

/// <summary>
/// Base class for integration events
/// Provides default implementation of IIntegrationEvent
/// Use 'record' keyword for immutability
/// 
/// Usage Example:
/// <code>
/// public sealed record EmployeeCreatedIntegrationEvent : IntegrationEvent
/// {
///     public Guid EmployeeId { get; init; }
///     public string FirstName { get; init; }
///     public string LastName { get; init; }
///     public string Email { get; init; }
///     public Guid CompanyId { get; init; }
///     public Guid? DepartmentId { get; init; }
///     public Guid? PositionId { get; init; }
/// }
/// </code>
/// 
/// Serialization Considerations:
/// - Will be serialized to JSON and stored in OutboxMessage.Content
/// - Must have public properties (for JSON serializer)
/// - Must be deserializable (public constructor or init-only properties)
/// - Keep payload small (avoid large objects, collections)
/// - Only include data needed by subscribers
/// - Use nullable types for optional data
/// 
/// Naming Convention:
/// - Use past tense (Created, Updated, Deleted, Changed)
/// - Suffix with "IntegrationEvent"
/// - Include source module/aggregate name
/// - Be specific about what happened
/// 
/// Good Examples:
/// - EmployeeCreatedIntegrationEvent (from Personnel)
/// - UserScopeLevelChangedIntegrationEvent (from Identity)
/// - DepartmentCreatedIntegrationEvent (from Organization)
/// - OperatorRegisteredIntegrationEvent (from Identity)
/// 
/// Bad Examples:
/// - EmployeeEvent (not specific)
/// - CreateEmployee (not past tense)
/// - EmployeeIntegrationEvent (too vague)
/// - EmployeeChangedIntegrationEvent (what changed?)
/// 
/// Versioning:
/// - If event structure changes, create new version
/// - Example: EmployeeCreatedIntegrationEventV2
/// - Keep old version for backward compatibility
/// - Or use event transformation/upcasting
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// Unique identifier of the integration event
    /// Generated automatically when event is created
    /// Used for idempotency checks in handlers
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the integration event occurred
    /// Set automatically to current UTC time
    /// Represents when the domain operation happened
    /// </summary>
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
