using HRM.BuildingBlocks.Domain.Abstractions.Events;
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
public abstract class ModuleDbContext : DbContext, IUnitOfWork
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
    /// Override SaveChangesAsync to update audit fields
    /// This is called by CommitAsync and can also be called directly by background services
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update audit timestamps before saving
        UpdateAuditFields();

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Update audit fields (CreatedAtUtc, ModifiedAtUtc) for tracked entities
    /// Called automatically before SaveChanges
    /// </summary>
    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries<Entity>();

        foreach (var entry in entries)
        {
            // Only update ModifiedAtUtc for modified entities
            // CreatedAtUtc is set in Entity constructor and never changed
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.MarkAsModified();
            }
        }
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
    }
}
