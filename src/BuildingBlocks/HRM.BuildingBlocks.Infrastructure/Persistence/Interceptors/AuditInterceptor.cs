using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor for automatic audit field updates.
///
/// Uses IExecutionContext (BuildingBlocks) — NOT ICurrentUserService (Identity module).
/// This keeps BuildingBlocks independent of Identity module.
/// Only needs IsAuthenticated and UserId — both available on IExecutionContext.
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
    private readonly IExecutionContext? _executionContext;

    /// <summary>
    /// Constructor with optional execution context.
    /// IExecutionContext may be null for background operations or seeding.
    /// </summary>
    /// <param name="executionContext">Execution context (may be null)</param>
    public AuditInterceptor(IExecutionContext? executionContext = null)
    {
        _executionContext = executionContext;
    }

    /// <summary>
    /// Called before synchronous SaveChanges
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
    /// Update audit fields for all added and modified entities.
    /// Only AuditableEntity (and descendants) have audit fields.
    /// </summary>
    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Get current user ID (may be null for anonymous operations)
        Guid? currentUserId = null;
        if (_executionContext?.IsAuthenticated == true)
        {
            try
            {
                currentUserId = _executionContext.UserId;
            }
            catch
            {
                // User not authenticated or error getting user ID
                // Continue without user tracking
            }
        }

        var entries = context.ChangeTracker
            .Entries<AuditableEntity>()
            .Where(entry => entry.State == EntityState.Added ||
                           entry.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (currentUserId.HasValue)
                {
                    entry.Entity.SetCreatedBy(currentUserId.Value);
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (currentUserId.HasValue)
                {
                    entry.Entity.MarkAsModified(currentUserId.Value);
                }
                else
                {
                    entry.Entity.MarkAsModified();
                }
            }
        }
    }
}
