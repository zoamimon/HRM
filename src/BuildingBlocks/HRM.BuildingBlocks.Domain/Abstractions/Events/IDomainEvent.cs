using MediatR;

namespace HRM.BuildingBlocks.Domain.Abstractions.Events;

/// <summary>
/// Interface for domain events in Domain-Driven Design
/// 
/// Domain events represent something that happened in the domain that domain experts care about
/// They capture the outcome of domain operations and enable reactive behavior
/// 
/// Characteristics:
/// - Past tense naming (OperatorRegistered, UserCreated, EmployeeAssigned)
/// - Immutable (use record type)
/// - Dispatched synchronously within the same transaction
/// - Handlers run in same transaction before commit
/// - Used for side effects within same bounded context/module
/// 
/// Dispatch Timing:
/// - Collected during entity operations
/// - Dispatched in UnitOfWork.CommitAsync() BEFORE SaveChanges
/// - All handlers complete before transaction commits
/// - If any handler fails, entire transaction rolls back
/// 
/// Handler Examples:
/// - OperatorRegisteredDomainEvent → Send welcome email
/// - OperatorRegisteredDomainEvent → Create integration event for audit
/// - UserCreatedDomainEvent → Create integration event for Personnel module
/// - EmployeeAssignedDomainEvent → Update department statistics
/// - UserScopeLevelChangedDomainEvent → Revoke active sessions
/// 
/// MediatR Integration:
/// - Extends INotification for MediatR dispatching
/// - Multiple handlers can subscribe to same event
/// - Handlers execute in registration order
/// - All handlers must complete successfully
/// 
/// Difference from Integration Events:
/// - Domain Events: Internal to module, synchronous, same transaction
/// - Integration Events: Cross-module, asynchronous, eventual consistency
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Unique identifier of the domain event
    /// Used for:
    /// - Event tracking and logging
    /// - Idempotency checks (if needed)
    /// - Event sourcing (if implemented)
    /// - Audit trail correlation
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// When the domain event occurred (UTC)
    /// Represents the exact moment the domain operation happened
    /// 
    /// Important for:
    /// - Event ordering (process in sequence)
    /// - Audit trail (when did this happen?)
    /// - Debugging (trace event timeline)
    /// - Event replay (if using event sourcing)
    /// </summary>
    DateTime OccurredOnUtc { get; }
}
