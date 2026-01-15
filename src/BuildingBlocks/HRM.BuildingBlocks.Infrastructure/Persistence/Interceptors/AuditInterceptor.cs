using HRM.BuildingBlocks.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor for automatic audit field updates
///
/// Responsibilities:
/// - Automatically update ModifiedAtUtc for modified entities
/// - Ensure CreatedAtUtc is set for new entities (already done in Entity constructor)
/// - No manual audit field management needed in application code
///
/// Benefits:
/// - Centralized audit logic
/// - Cannot be forgotten or bypassed
/// - Consistent audit trail across entire application
/// - Reduces boilerplate in domain/application layers
///
/// How It Works:
/// 1. Intercepts SaveChanges/SaveChangesAsync calls
/// 2. Scans ChangeTracker for modified entities
/// 3. Updates ModifiedAtUtc for EntityState.Modified entities
/// 4. Continues with normal SaveChanges execution
///
/// Usage:
/// Add to DbContext options in Startup/Program.cs:
///
/// <code>
/// services.AddDbContext<IdentityDbContext>(options =>
/// {
///     options.UseSqlServer(connectionString);
///     options.AddInterceptors(new AuditInterceptor());
/// });
/// </code>
///
/// Note: ModuleDbContext also has UpdateAuditFields() method for backward compatibility
/// Using interceptor is preferred approach in modern EF Core
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
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
    /// Update audit timestamps for all modified entities
    /// Only updates ModifiedAtUtc (CreatedAtUtc is immutable, set in constructor)
    /// </summary>
    /// <param name="context">DbContext with tracked entities</param>
    private static void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Get all modified entities inheriting from Entity base class
        var modifiedEntries = context.ChangeTracker
            .Entries<Entity>()
            .Where(entry => entry.State == EntityState.Modified);

        // Update ModifiedAtUtc for each modified entity
        foreach (var entry in modifiedEntries)
        {
            entry.Entity.MarkAsModified();
        }
    }
}
