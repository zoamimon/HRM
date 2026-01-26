namespace HRM.Modules.Identity.Domain.Repositories;

/// <summary>
/// Repository interface for querying operator permissions
///
/// Design:
/// - Read-only queries for authorization
/// - Optimized for permission checking (not CRUD operations)
/// - Used by PermissionService for authorization
///
/// Data Flow:
/// Operator -> OperatorRoles -> Roles -> RolePermissions
///
/// Usage:
/// <code>
/// var permissions = await repository.GetPermissionsAsync(operatorId);
/// var isSuperAdmin = await repository.IsSuperAdminAsync(operatorId);
/// </code>
/// </summary>
public interface IOperatorPermissionRepository
{
    /// <summary>
    /// Get all permission keys for an operator
    /// Returns set of "Module.Entity.Action" strings
    /// </summary>
    /// <param name="operatorId">Operator ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Set of permission keys</returns>
    Task<HashSet<string>> GetPermissionsAsync(
        Guid operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if operator has the "System Administrator" role
    /// System Administrator has all permissions (super admin)
    /// </summary>
    /// <param name="operatorId">Operator ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if operator is super admin</returns>
    Task<bool> IsSuperAdminAsync(
        Guid operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if operator has specific permission
    /// More efficient than loading all permissions
    /// </summary>
    /// <param name="operatorId">Operator ID</param>
    /// <param name="module">Module name</param>
    /// <param name="entity">Entity name</param>
    /// <param name="action">Action name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if operator has the permission</returns>
    Task<bool> HasPermissionAsync(
        Guid operatorId,
        string module,
        string entity,
        string action,
        CancellationToken cancellationToken = default);
}
