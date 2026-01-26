using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Application.Abstractions.Authorization;

/// <summary>
/// Service for checking user permissions
///
/// Design:
/// - Abstraction for permission checking logic
/// - Implementation in Identity.Infrastructure (has access to user roles/permissions)
/// - Used by PermissionAuthorizationHandler
///
/// Permission Model:
/// - Users have Roles
/// - Roles have Permissions (Module.Entity.Action + Scope)
/// - Scope determines data visibility (Company, Department, Position, Self)
///
/// Usage:
/// <code>
/// // Check if user has permission
/// var hasPermission = await permissionService.HasPermissionAsync(
///     userId, "Personnel", "Employee", "View");
///
/// // Get user's scope for a permission
/// var scope = await permissionService.GetPermissionScopeAsync(
///     userId, "Personnel", "Employee", "View");
/// </code>
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Check if user has specific permission (any scope)
    /// </summary>
    /// <param name="userId">User ID (Guid as string)</param>
    /// <param name="module">Module name</param>
    /// <param name="entity">Entity name</param>
    /// <param name="action">Action name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has the permission</returns>
    Task<bool> HasPermissionAsync(
        string userId,
        string module,
        string entity,
        string action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user's scope for a specific permission
    /// Returns null if user doesn't have the permission
    /// </summary>
    /// <param name="userId">User ID (Guid as string)</param>
    /// <param name="module">Module name</param>
    /// <param name="entity">Entity name</param>
    /// <param name="action">Action name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scope level or null if no permission</returns>
    Task<ScopeLevel?> GetPermissionScopeAsync(
        string userId,
        string module,
        string entity,
        string action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions for a user
    /// Used for caching or displaying in UI
    /// </summary>
    /// <param name="userId">User ID (Guid as string)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Set of permission strings (Module.Entity.Action)</returns>
    Task<HashSet<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user is a super admin (bypasses all permission checks)
    /// </summary>
    /// <param name="userId">User ID (Guid as string)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user is super admin</returns>
    Task<bool> IsSuperAdminAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
