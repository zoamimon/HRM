using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor for automatic audit field updates
///
/// Responsibilities:
/// - Automatically update ModifiedAtUtc and ModifiedById for modified entities
/// - Automatically set CreatedById for new entities
/// - Track WHO made changes (CreatedBy/ModifiedBy)
/// - No manual audit field management needed in application code
///
/// Benefits:
/// - Centralized audit logic
/// - Cannot be forgotten or bypassed
/// - Consistent audit trail across entire application
/// - Tracks both WHEN (timestamps) and WHO (user IDs)
/// - Reduces boilerplate in domain/application layers
///
/// How It Works:
/// 1. Intercepts SaveChanges/SaveChangesAsync calls
/// 2. Gets current user ID from ICurrentUserService
/// 3. Updates CreatedById for EntityState.Added entities
/// 4. Updates ModifiedById and ModifiedAtUtc for EntityState.Modified entities
/// 5. Continues with normal SaveChanges execution
///
/// Usage:
/// Register as singleton in DI (already done in InfrastructureServiceExtensions):
///
/// <code>
/// services.AddSingleton<AuditInterceptor>();
///
/// services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
/// {
///     options.UseSqlServer(connectionString);
///     options.AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());
/// });
/// </code>
///
/// Audit Fields:
/// - CreatedAtUtc: Set in Entity constructor
/// - CreatedById: Set by this interceptor on insert
/// - ModifiedAtUtc: Set by this interceptor on update
/// - ModifiedById: Set by this interceptor on update
///
/// Anonymous Operations:
/// If no user is authenticated (e.g., background jobs, seeding):
/// - CreatedById and ModifiedById remain NULL
/// - Timestamps are still set correctly
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService? _currentUserService;

    /// <summary>
    /// Constructor with optional current user service
    /// ICurrentUserService may be null for background operations or seeding
    /// </summary>
    /// <param name="currentUserService">Current user service (may be null)</param>
    public AuditInterceptor(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }
    /// <summary>
    /// Called before synchronous SaveChanges
    /// Updates audit fields for modified entities
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Called before asynchronous SaveChangesAsync
    /// Updates audit fields for modified entities
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Update audit fields for all added and modified entities
    /// - Added entities: Set CreatedById
    /// - Modified entities: Set ModifiedAtUtc and ModifiedById
    ///
    /// Note: Only AuditableEntity (and its descendants) have audit fields.
    /// Plain Entity (minimal) does not have audit capabilities.
    /// </summary>
    /// <param name="context">DbContext with tracked entities</param>
    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Get current user ID (may be null for anonymous operations)
        Guid? currentUserId = null;
        if (_currentUserService?.IsAuthenticated == true)
        {
            try
            {
                currentUserId = _currentUserService.UserId;
            }
            catch
            {
                // User not authenticated or error getting user ID
                // Continue without user tracking
            }
        }

        // Get all added and modified AuditableEntity instances
        // Note: Only AuditableEntity has audit fields, not Entity
        var entries = context.ChangeTracker
            .Entries<AuditableEntity>()
            .Where(entry => entry.State == EntityState.Added ||
                           entry.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                // New entity: Set CreatedById
                if (currentUserId.HasValue)
                {
                    entry.Entity.SetCreatedBy(currentUserId.Value);
                }
                // CreatedAtUtc already set in AuditableEntity constructor
            }
            else if (entry.State == EntityState.Modified)
            {
                // Modified entity: Set ModifiedAtUtc and ModifiedById
                if (currentUserId.HasValue)
                {
                    entry.Entity.MarkAsModified(currentUserId.Value);
                }
                else
                {
                    // No user context, just update timestamp
                    entry.Entity.MarkAsModified();
                }
            }
        }
    }
}
