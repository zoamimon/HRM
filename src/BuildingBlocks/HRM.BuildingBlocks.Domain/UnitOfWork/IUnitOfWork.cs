using HRM.BuildingBlocks.Domain.Database;

namespace HRM.BuildingBlocks.Domain.UnitOfWork;

/// <summary>
/// Unit of Work pattern interface
/// Ensures transactional consistency for all domain operations
/// Implemented by each module's DbContext
/// 
/// ✅ FIXED: Extends IModuleContext (pure domain) instead of IModuleDbContext (infrastructure)
/// 
/// Core Responsibilities:
/// 1. Collect domain events from tracked entities
/// 2. Dispatch domain events synchronously via MediatR
/// 3. Domain event handlers create integration events → OutboxMessages
/// 4. Save all changes in single atomic transaction
/// 5. Clear domain events after successful dispatch
/// 
/// Transaction Guarantee:
/// - Domain entities + OutboxMessages saved atomically
/// - If any part fails, entire transaction rolls back
/// - No partial commits (all or nothing)
/// - Database consistency maintained
/// 
/// Example Flow - Register Operator:
/// 
/// 1. Application Layer (Command Handler):
///    <code>
///    var operator = Operator.Register(username, email, hashedPassword);
///    // Domain event raised: OperatorRegisteredDomainEvent
///    
///    await _operatorRepository.AddAsync(operator);
///    await _unitOfWork.CommitAsync(); // ← Triggers the workflow below
///    </code>
/// 
/// 2. UnitOfWork.CommitAsync() Implementation:
///    <code>
///    // Step 1: Collect domain events
///    var domainEvents = ChangeTracker.Entries<Entity>()
///        .SelectMany(e => e.Entity.DomainEvents);
///    
///    // Step 2: Dispatch events (synchronous, before commit)
///    foreach (var evt in domainEvents)
///        await _publisher.Publish(evt); // MediatR
///    
///    // Step 3: Handler creates integration event
///    // OperatorRegisteredDomainEventHandler:
///    //   - Creates OperatorRegisteredIntegrationEvent
///    //   - Serializes to JSON
///    //   - Creates OutboxMessage
///    //   - Adds to OutboxMessages DbSet
///    
///    // Step 4: Save everything (atomic)
///    await base.SaveChangesAsync();
///    // Commits: Operator + OutboxMessage
///    
///    // Step 5: Clear events
///    foreach (var entity in ChangeTracker.Entries<Entity>())
///        entity.Entity.ClearDomainEvents();
///    </code>
/// 
/// 3. Result:
///    ✅ Operator saved to [identity].[Operators]
///    ✅ OutboxMessage saved to [identity].[OutboxMessages]
///    ✅ Both in SAME transaction (atomic!)
///    ✅ Background service will publish integration event later
/// 
/// Benefits:
/// - Transactional consistency (ACID properties)
/// - Reliable event publishing (outbox pattern)
/// - Clean separation of concerns
/// - Testable (can mock IUnitOfWork)
/// - Domain events enable reactive behavior
/// - Integration events enable cross-module communication
/// 
/// Why Extends IModuleContext:
/// - DbContext implements both IUnitOfWork and IModuleContext
/// - Background service needs IModuleContext for basic operations
/// - No EF Core types in Domain layer (clean architecture)
/// - Infrastructure layer adds IModuleDbContext with EF Core types
/// </summary>
public interface IUnitOfWork : IModuleContext
{
    /// <summary>
    /// Commit all pending changes to database with domain event dispatch
    /// 
    /// Detailed Workflow:
    /// 
    /// 1. Collect Domain Events:
    ///    - Scan ChangeTracker for entities inheriting from Entity
    ///    - Extract all domain events from entity.DomainEvents collection
    ///    - Events raised by: entity.AddDomainEvent() calls
    /// 
    /// 2. Dispatch Domain Events (Synchronous):
    ///    - Use MediatR.IPublisher.Publish()
    ///    - Events dispatched in order they were raised
    ///    - All handlers execute BEFORE SaveChanges
    ///    - Handlers run in SAME transaction
    ///    - Handlers can:
    ///      * Modify existing entities
    ///      * Create new entities
    ///      * Create integration events → OutboxMessages
    ///      * Raise additional domain events
    /// 
    /// 3. Domain Event Handlers Create Integration Events:
    ///    Example Handler:
    ///    <code>
    ///    public class OperatorRegisteredDomainEventHandler 
    ///        : INotificationHandler<OperatorRegisteredDomainEvent>
    ///    {
    ///        private readonly IdentityDbContext _context;
    ///        
    ///        public async Task Handle(OperatorRegisteredDomainEvent evt, ...)
    ///        {
    ///            // Create integration event
    ///            var integrationEvent = new OperatorRegisteredIntegrationEvent
    ///            {
    ///                OperatorId = evt.OperatorId,
    ///                Username = evt.Username,
    ///                Email = evt.Email
    ///            };
    ///            
    ///            // Serialize to JSON
    ///            var json = JsonSerializer.Serialize(integrationEvent);
    ///            
    ///            // Create outbox message
    ///            var outboxMessage = OutboxMessage.Create(
    ///                type: integrationEvent.GetType().AssemblyQualifiedName,
    ///                content: json
    ///            );
    ///            
    ///            // Add to DbContext (will be saved in step 4)
    ///            await _context.OutboxMessages.AddAsync(outboxMessage);
    ///            
    ///            // No SaveChanges here! Will be done in CommitAsync
    ///        }
    ///    }
    ///    </code>
    /// 
    /// 4. Save All Changes (Atomic Transaction):
    ///    - Call base.SaveChangesAsync()
    ///    - Commits ALL tracked changes:
    ///      * Domain entities (Operator, User, Employee, etc.)
    ///      * OutboxMessages
    ///      * Any entities created by domain event handlers
    ///    - All in SINGLE database transaction
    ///    - If ANY part fails, ENTIRE transaction rolls back
    /// 
    /// 5. Clear Domain Events:
    ///    - Call entity.ClearDomainEvents() on all tracked entities
    ///    - Prevents duplicate dispatch if CommitAsync called again
    ///    - Clean state for next operation
    /// 
    /// Error Handling:
    /// - If domain event handler throws: Transaction rolled back
    /// - If SaveChanges throws: Transaction rolled back
    /// - If any step fails: All changes discarded
    /// 
    /// Returns:
    /// - Number of state entries written to database
    /// - Includes both domain entities and outbox messages
    /// 
    /// Throws:
    /// - DbUpdateException: Database constraint violations
    /// - DbUpdateConcurrencyException: Optimistic concurrency conflicts
    /// - Exception: Domain event handler exceptions
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of affected rows in database</returns>
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
