namespace HRM.BuildingBlocks.Domain.Abstractions.Events;

/// <summary>
/// Base class for domain events
/// Provides default implementation of IDomainEvent
/// Use 'record' keyword for immutability
/// 
/// Usage Example:
/// <code>
/// public sealed record OperatorRegisteredDomainEvent : DomainEvent
/// {
///     public Guid OperatorId { get; init; }
///     public string Username { get; init; }
///     public string Email { get; init; }
/// }
/// </code>
/// 
/// Benefits of using 'record':
/// - Immutable by default (init-only properties)
/// - Value-based equality (compared by content, not reference)
/// - With-expressions for creating modified copies
/// - Concise syntax with positional parameters
/// - Built-in ToString() with property values
/// 
/// Naming Convention:
/// - Use past tense (Registered, Created, Updated, Deleted)
/// - Suffix with "DomainEvent"
/// - Be specific about what happened
/// - Include aggregate root name
/// 
/// Good Examples:
/// - OperatorRegisteredDomainEvent
/// - UserCreatedDomainEvent
/// - EmployeeAssignedDomainEvent
/// - DepartmentRenamedDomainEvent
/// 
/// Bad Examples:
/// - OperatorEvent (not specific)
/// - RegisterOperator (not past tense)
/// - OperatorChanged (too vague)
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <summary>
    /// Unique identifier of the domain event
    /// Generated automatically when event is created
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the domain event occurred
    /// Set automatically to current UTC time
    /// Ensures consistency across time zones
    /// </summary>
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
