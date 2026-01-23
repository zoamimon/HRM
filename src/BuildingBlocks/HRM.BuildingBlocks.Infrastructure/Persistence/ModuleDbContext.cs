using System.Linq.Expressions;
using HRM.BuildingBlocks.Domain.Abstractions.Events;
using HRM.BuildingBlocks.Domain.Abstractions.SoftDelete;
using HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork;
using HRM.BuildingBlocks.Domain.Entities;
using HRM.BuildingBlocks.Domain.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for all modules implementing UnitOfWork pattern
///
/// Responsibilities:
/// 1. Provide DbSet for OutboxMessages (transactional outbox pattern)
/// 2. Implement IUnitOfWork.CommitAsync() with domain event dispatch
/// 3. Implement IModuleContext for basic database operations
/// 4. Automatic audit field updates (CreatedAtUtc, ModifiedAtUtc)
///
/// Usage Pattern:
/// Each module creates its own DbContext inheriting from this:
///
/// <code>
/// public class IdentityDbContext : ModuleDbContext
/// {
///     public override string ModuleName => "Identity";
///
///     public DbSet<Operator> Operators => Set<Operator>();
///     public DbSet<User> Users => Set<User>();
///
///     public IdentityDbContext(DbContextOptions<IdentityDbContext> options, IPublisher publisher)
///         : base(options, publisher)
///     {
///     }
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         modelBuilder.HasDefaultSchema("identity");
///         modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
///     }
/// }
/// </code>
///
/// Key Features:
/// - Domain event dispatch BEFORE SaveChanges (synchronous, in transaction)
/// - OutboxMessage creation by domain event handlers (in same transaction)
/// - Automatic audit trail (CreatedAtUtc, ModifiedAtUtc)
/// - Transaction rollback on any failure
/// - Clean separation of domain and integration events
/// </summary>
public abstract class ModuleDbContext : DbContext, IModuleUnitOfWork
{
    private readonly IPublisher _publisher;

    /// <summary>
    /// Module name for identification
    /// Must be overridden by derived classes
    ///
    /// Examples: "Identity", "Personnel", "Organization"
    /// </summary>
    public abstract string ModuleName { get; }

    /// <summary>
    /// OutboxMessages table for this module
    /// Each module has its own outbox table for isolation
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Protected constructor for derived module DbContexts
    /// </summary>
    /// <param name="options">DbContext options (connection string, etc.)</param>
    /// <param name="publisher">MediatR publisher for domain events</param>
    protected ModuleDbContext(DbContextOptions options, IPublisher publisher)
        : base(options)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// Commit all changes with domain event dispatch
    ///
    /// Workflow:
    /// 1. Collect domain events from tracked entities
    /// 2. Dispatch domain events synchronously (BEFORE SaveChanges)
    /// 3. Domain event handlers create OutboxMessages
    /// 4. Save all changes in single transaction (entities + outbox messages)
    /// 5. Clear domain events from entities
    ///
    /// Transaction Guarantee:
    /// - All changes saved atomically
    /// - If any step fails, entire transaction rolls back
    /// - No partial commits
    ///
    /// Example Timeline:
    /// T+0ms:   CommitAsync() called
    /// T+1ms:   Collect domain events (OperatorRegisteredDomainEvent)
    /// T+2ms:   Dispatch event to handlers
    /// T+5ms:   Handler creates OperatorRegisteredIntegrationEvent
    /// T+6ms:   Handler creates OutboxMessage
    /// T+7ms:   SaveChanges() saves Operator + OutboxMessage
    /// T+10ms:  Clear domain events
    /// T+11ms:  Return success
    ///
    /// Later:
    /// T+60s:   OutboxProcessor publishes integration event
    /// </summary>
    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: Collect domain events from all tracked entities
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToList();

        // Step 2: Dispatch domain events synchronously (in transaction)
        // Handlers can create OutboxMessages which will be saved in step 3
        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }

        // Step 3: Save all changes atomically
        // - Domain entities (Operator, User, Employee, etc.)
        // - OutboxMessages created by domain event handlers
        // - All in SINGLE database transaction
        var result = await base.SaveChangesAsync(cancellationToken);

        // Step 4: Clear domain events to prevent duplicate dispatch
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            entry.Entity.ClearDomainEvents();
        }

        return result;
    }

    /// <summary>
    /// Override SaveChangesAsync - audit fields handled by AuditInterceptor
    /// This is called by CommitAsync and can also be called directly by background services
    ///
    /// Note: Audit field updates (CreatedById, ModifiedById, ModifiedAtUtc) are now
    /// handled by AuditInterceptor which is registered as an EF Core SaveChangesInterceptor.
    /// The interceptor has access to ICurrentUserService for user tracking.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Audit updates handled by AuditInterceptor
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Configure conventions for all modules
    /// Override in derived classes to add module-specific configurations
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure OutboxMessage entity
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Content)
                .IsRequired();

            entity.Property(e => e.OccurredOnUtc)
                .IsRequired();

            entity.Property(e => e.Error)
                .HasMaxLength(2000);

            // Index for efficient querying of unprocessed messages
            entity.HasIndex(e => e.ProcessedOnUtc)
                .HasDatabaseName("IX_OutboxMessages_ProcessedOnUtc");

            // Index for ordering by occurrence time
            entity.HasIndex(e => e.OccurredOnUtc)
                .HasDatabaseName("IX_OutboxMessages_OccurredOnUtc");
        });

        // Configure global query filters for soft delete
        // Automatically exclude soft-deleted entities from all queries
        // Can be disabled per query with: query.IgnoreQueryFilters()
        ConfigureSoftDeleteQueryFilter(modelBuilder);
    }

    /// <summary>
    /// Configure global query filter to exclude soft-deleted entities
    /// Applies to all entities implementing ISoftDeletable
    ///
    /// How It Works:
    /// 1. Iterate through all entity types in the model
    /// 2. Check if entity implements ISoftDeletable interface
    /// 3. Build expression: e => e.IsDeleted == false
    /// 4. Set as global query filter
    ///
    /// Effect:
    /// - All queries automatically filter: WHERE IsDeleted = 0
    /// - Applies to: Find, FirstOrDefault, Where, ToList, etc.
    /// - Cascades to navigation properties
    ///
    /// Override Filter:
    /// To include soft-deleted entities in specific queries:
    /// <code>
    /// var allEmployees = await context.Employees
    ///     .IgnoreQueryFilters()  // Include soft-deleted
    ///     .ToListAsync();
    /// </code>
    ///
    /// Benefits:
    /// - Prevents accidental access to deleted data
    /// - Consistent behavior across application
    /// - No manual WHERE clauses needed
    /// - Safer than manual filtering
    ///
    /// Example Generated SQL:
    /// <code>
    /// -- Before (manual):
    /// SELECT * FROM Employees WHERE IsDeleted = 0 AND Department = 'IT'
    ///
    /// -- After (automatic):
    /// SELECT * FROM Employees WHERE IsDeleted = 0 AND Department = 'IT'
    /// </code>
    /// </summary>
    /// <param name="modelBuilder">Model builder</param>
    private void ConfigureSoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        // Get all entity types that implement ISoftDeletable
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                // Build expression: e => e.IsDeleted == false
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var filterExpression = Expression.Lambda(
                    Expression.Equal(property, Expression.Constant(false)),
                    parameter
                );

                // Set as global query filter
                entityType.SetQueryFilter(filterExpression);
            }
        }
    }
}
