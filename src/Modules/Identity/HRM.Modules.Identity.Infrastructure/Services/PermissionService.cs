using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using HRM.BuildingBlocks.Domain.Enums;
using HRM.Modules.Identity.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of IPermissionService for Identity module
///
/// Design:
/// - Checks user permissions from database (via repositories)
/// - Caches permissions for performance
/// - Supports super admin bypass
///
/// Permission Model:
/// - Operators have Roles (via OperatorRoles junction)
/// - Roles have Permissions (via RolePermissions)
/// - Permission = Module.Entity.Action + Scope
///
/// Caching:
/// - User permissions cached for 5 minutes
/// - Cache invalidated on role/permission changes
///
/// TODO: Implement actual database queries when Role-Permission relationship is set up
/// Currently returns placeholder implementation
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IOperatorRepository _operatorRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;

    private const string PermissionCacheKeyPrefix = "UserPermissions_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public PermissionService(
        IOperatorRepository operatorRepository,
        IMemoryCache cache,
        ILogger<PermissionService> logger)
    {
        _operatorRepository = operatorRepository ?? throw new ArgumentNullException(nameof(operatorRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check if user has specific permission
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        string userId,
        string module,
        string entity,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        // Check super admin first
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return true;
        }

        // Get user permissions (cached)
        var permissions = await GetUserPermissionsAsync(userId, cancellationToken);
        var permissionKey = $"{module}.{entity}.{action}";

        return permissions.Contains(permissionKey);
    }

    /// <summary>
    /// Get user's scope for a permission
    /// </summary>
    public async Task<ScopeLevel?> GetPermissionScopeAsync(
        string userId,
        string module,
        string entity,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        // Super admin has company-wide scope
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return ScopeLevel.Company;
        }

        // TODO: Implement actual scope lookup from database
        // For now, return Company scope if user has permission
        var hasPermission = await HasPermissionAsync(userId, module, entity, action, cancellationToken);

        return hasPermission ? ScopeLevel.Company : null;
    }

    /// <summary>
    /// Get all permissions for a user
    /// </summary>
    public async Task<HashSet<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var cacheKey = $"{PermissionCacheKeyPrefix}{userId}";

        // Try get from cache
        if (_cache.TryGetValue<HashSet<string>>(cacheKey, out var cachedPermissions) && cachedPermissions != null)
        {
            return cachedPermissions;
        }

        // Load from database
        var permissions = await LoadUserPermissionsFromDatabaseAsync(userId, cancellationToken);

        // Cache for future requests
        _cache.Set(cacheKey, permissions, CacheDuration);

        return permissions;
    }

    /// <summary>
    /// Check if user is super admin
    /// </summary>
    public async Task<bool> IsSuperAdminAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var operatorId))
        {
            return false;
        }

        var @operator = await _operatorRepository.GetByIdAsync(operatorId, cancellationToken);

        // Check if operator exists and is the system admin
        // System admin is typically the first operator created with username "admin"
        return @operator?.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Invalidate permission cache for a user
    /// Call this when user's roles or permissions change
    /// </summary>
    public void InvalidateCache(string userId)
    {
        var cacheKey = $"{PermissionCacheKeyPrefix}{userId}";
        _cache.Remove(cacheKey);

        _logger.LogDebug("Permission cache invalidated for user {UserId}", userId);
    }

    #region Private Methods

    /// <summary>
    /// Load user permissions from database
    /// TODO: Implement when Role-Permission tables are ready
    /// </summary>
    private async Task<HashSet<string>> LoadUserPermissionsFromDatabaseAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Guid.TryParse(userId, out var operatorId))
        {
            return permissions;
        }

        var @operator = await _operatorRepository.GetByIdAsync(operatorId, cancellationToken);
        if (@operator == null)
        {
            return permissions;
        }

        // TODO: Replace with actual database query
        // For now, grant all Identity module permissions to active operators
        // This is a placeholder until Role-Permission relationship is implemented

        if (@operator.GetIsActive())
        {
            // Identity module permissions for all active operators
            permissions.Add("Identity.User.View");
            permissions.Add("Identity.Role.View");

            // Admin gets more permissions
            if (@operator.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                // Full Identity permissions
                permissions.Add("Identity.User.Create");
                permissions.Add("Identity.User.Update");
                permissions.Add("Identity.User.Delete");
                permissions.Add("Identity.User.ResetPassword");
                permissions.Add("Identity.User.AssignPermission");
                permissions.Add("Identity.Operator.View");
                permissions.Add("Identity.Operator.Create");
                permissions.Add("Identity.Operator.Update");
                permissions.Add("Identity.Operator.Delete");
                permissions.Add("Identity.Operator.ResetPassword");
                permissions.Add("Identity.Operator.AssignPermission");
                permissions.Add("Identity.Role.Create");
                permissions.Add("Identity.Role.Update");
                permissions.Add("Identity.Role.Delete");
                permissions.Add("Identity.Role.AssignPermission");
            }
        }

        _logger.LogDebug(
            "Loaded {Count} permissions for user {UserId}",
            permissions.Count,
            userId);

        return permissions;
    }

    #endregion
}
