namespace HRM.BuildingBlocks.Domain.Abstractions.Database;

/// <summary>
/// Pure domain interface for module context
/// NO infrastructure dependencies (EF Core, Dapper, etc.)
/// 
/// Purpose:
/// - Define minimal contract for module data persistence
/// - Keep Domain layer clean (no infrastructure leakage)
/// - Enable different persistence implementations (EF Core, Dapper, etc.)
/// 
/// Implementing Classes:
/// - IdentityDbContext (Identity module)
/// - PersonnelDbContext (Personnel module)
/// - OrganizationDbContext (Organization module)
/// 
/// Why Separate from IModuleDbContext:
/// - Domain layer should NOT depend on EF Core types (DbSet, EntityEntry)
/// - IModuleDbContext with EF Core types belongs in Infrastructure layer
/// - This interface is PURE domain (can be in Domain layer)
/// - Clean Architecture: Domain → Application → Infrastructure
/// 
/// Infrastructure Extension:
/// - HRM.BuildingBlocks.Infrastructure defines IModuleDbContext : IModuleContext
/// - IModuleDbContext adds EF Core specific members (DbSet, EntityEntry)
/// - Background service uses IModuleDbContext (infrastructure concern)
/// - Domain/Application use IModuleContext (pure concern)
/// </summary>
public interface IModuleContext
{
    /// <summary>
    /// Module name for identification and logging
    /// 
    /// Examples:
    /// - "Identity" for IdentityDbContext
    /// - "Personnel" for PersonnelDbContext
    /// - "Organization" for OrganizationDbContext
    /// 
    /// Naming Convention:
    /// - PascalCase
    /// - Singular (not "Identities")
    /// - Matches module folder name
    /// - Used in logs, events, and monitoring
    /// 
    /// Usage:
    /// - Logging: "Processing outbox in {ModuleName} module"
    /// - Events: Track which module raised which events
    /// - Monitoring: Per-module metrics
    /// - Debugging: Identify data source
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Save changes to database
    /// 
    /// Responsibilities:
    /// - Persist tracked entity changes
    /// - Execute database commands
    /// - Handle concurrency conflicts
    /// - Return number of affected rows
    /// 
    /// Does NOT:
    /// - Dispatch domain events (done in CommitAsync)
    /// - Create integration events (done in domain event handlers)
    /// - Manage transactions (caller's responsibility)
    /// 
    /// Usage Contexts:
    /// 
    /// 1. Called by UnitOfWork.CommitAsync():
    ///    - After dispatching domain events
    ///    - Saves domain entities + outbox messages
    ///    - All in one transaction
    /// 
    /// 2. Called by Background Service:
    ///    - After updating OutboxMessage status
    ///    - No domain events dispatched
    ///    - Only saves outbox message updates
    /// 
    /// Returns:
    /// - Number of state entries written to database
    /// - 0 if no changes detected
    /// - Positive integer for successful saves
    /// 
    /// Exceptions:
    /// - DbUpdateException: Concurrency conflicts, constraint violations
    /// - DbUpdateConcurrencyException: Optimistic concurrency failure
    /// - Exception: General database errors
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of affected rows</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
