using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using HRM.BuildingBlocks.Domain.Abstractions.Security;
using HRM.BuildingBlocks.Domain.Enums;
using HRM.Modules.Identity.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HRM.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of IPermissionService for Identity module
///
/// Design:
/// - Queries permissions from database via IOperatorPermissionRepository
/// - Caches permissions for performance (5 minutes TTL)
/// - Super admin bypass via "System Administrator" role
///
/// Permission Model:
/// - Operators have Roles (via OperatorRoles junction table)
/// - Roles have Permissions (via RolePermissions table)
/// - Permission = Module.Entity.Action
///
/// Data Flow:
/// Operator -> OperatorRoles -> Roles -> RolePermissions
///
/// Caching Strategy:
/// - User permissions cached for 5 minutes
/// - Super admin status cached for 5 minutes
/// - Call InvalidateCache() when roles/permissions change
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IOperatorPermissionRepository _permissionRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionService> _logger;

    private const string PermissionCacheKeyPrefix = "UserPermissions_";
    private const string SuperAdminCacheKeyPrefix = "IsSuperAdmin_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public PermissionService(
        IOperatorPermissionRepository permissionRepository,
        IMemoryCache cache,
        ILogger<PermissionService> logger)
    {
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
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
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var operatorId))
        {
            return false;
        }

        // Check super admin first (bypass all permission checks)
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            _logger.LogDebug(
                "Super admin bypass for user {UserId}, permission {Module}.{Entity}.{Action}",
                userId, module, entity, action);
            return true;
        }

        // Get user permissions (cached)
        var permissions = await GetUserPermissionsAsync(userId, cancellationToken);
        var permissionKey = $"{module}.{entity}.{action}";

        var hasPermission = permissions.Contains(permissionKey);

        _logger.LogDebug(
            "Permission check for user {UserId}: {Module}.{Entity}.{Action} = {Result}",
            userId, module, entity, action, hasPermission);

        return hasPermission;
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
        // Currently returns Company scope if user has permission
        // Future: Query RolePermissions.Scope for the specific permission
        var hasPermission = await HasPermissionAsync(userId, module, entity, action, cancellationToken);

        return hasPermission ? ScopeLevel.Company : null;
    }

    /// <summary>
    /// Get all permissions for a user
    /// Uses caching for performance
    /// </summary>
    public async Task<HashSet<string>> GetUserPermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var operatorId))
        {
            return [];
        }

        var cacheKey = $"{PermissionCacheKeyPrefix}{userId}";

        // Try get from cache
        if (_cache.TryGetValue<HashSet<string>>(cacheKey, out var cachedPermissions) && cachedPermissions != null)
        {
            _logger.LogDebug("Cache hit for user {UserId} permissions", userId);
            return cachedPermissions;
        }

        // Load from database
        _logger.LogDebug("Cache miss for user {UserId} permissions, loading from database", userId);
        var permissions = await _permissionRepository.GetPermissionsAsync(operatorId, cancellationToken);

        // Cache for future requests
        _cache.Set(cacheKey, permissions, CacheDuration);

        _logger.LogDebug(
            "Loaded {Count} permissions for user {UserId}",
            permissions.Count,
            userId);

        return permissions;
    }

    /// <summary>
    /// Check if user is super admin (has "System Administrator" role)
    /// Uses caching for performance
    /// </summary>
    public async Task<bool> IsSuperAdminAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var operatorId))
        {
            return false;
        }

        var cacheKey = $"{SuperAdminCacheKeyPrefix}{userId}";

        // Try get from cache
        if (_cache.TryGetValue<bool>(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        // Query database
        var isSuperAdmin = await _permissionRepository.IsSuperAdminAsync(operatorId, cancellationToken);

        // Cache for future requests
        _cache.Set(cacheKey, isSuperAdmin, CacheDuration);

        if (isSuperAdmin)
        {
            _logger.LogDebug("User {UserId} is super admin", userId);
        }

        return isSuperAdmin;
    }

    /// <summary>
    /// Invalidate permission cache for a user
    /// Call this when user's roles or permissions change
    /// </summary>
    public void InvalidateCache(string userId)
    {
        var permissionCacheKey = $"{PermissionCacheKeyPrefix}{userId}";
        var superAdminCacheKey = $"{SuperAdminCacheKeyPrefix}{userId}";
        var permissionScopeCacheKey = $"{PermissionScopeCacheKeyPrefix}{userId}";

        _cache.Remove(permissionCacheKey);
        _cache.Remove(superAdminCacheKey);
        _cache.Remove(permissionScopeCacheKey);

        _logger.LogInformation("Permission cache invalidated for user {UserId}", userId);
    }

    // ================================================================
    // New methods for PermissionScope-based authorization
    // ================================================================

    private const string PermissionScopeCacheKeyPrefix = "UserPermissionScopes_";

    /// <summary>
    /// Check if user has permission with the specified key format
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        string userId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        // Parse permission key (Module.Entity.Action)
        var parts = permissionKey.Split('.');
        if (parts.Length != 3)
        {
            _logger.LogWarning("Invalid permission key format: {PermissionKey}", permissionKey);
            return false;
        }

        return await HasPermissionAsync(userId, parts[0], parts[1], parts[2], cancellationToken);
    }

    /// <summary>
    /// Check if user has permission with at least the specified scope level
    /// </summary>
    public async Task<bool> HasPermissionWithScopeAsync(
        string userId,
        string permissionKey,
        PermissionScope minScope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        // Super admin has Global scope for all permissions
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            _logger.LogDebug(
                "Super admin bypass for user {UserId}, permission {PermissionKey}",
                userId, permissionKey);
            return true;
        }

        // Get user's scope for this permission
        var userScope = await GetPermissionScopeAsync(userId, permissionKey, cancellationToken);

        if (!userScope.HasValue)
        {
            _logger.LogDebug(
                "User {UserId} does not have permission {PermissionKey}",
                userId, permissionKey);
            return false;
        }

        // Check if user's scope is >= required scope
        var hasScope = (int)userScope.Value >= (int)minScope;

        _logger.LogDebug(
            "Scope check for user {UserId}: {PermissionKey} userScope={UserScope} >= minScope={MinScope} = {Result}",
            userId, permissionKey, userScope.Value, minScope, hasScope);

        return hasScope;
    }

    /// <summary>
    /// Get user's scope for a specific permission key
    /// </summary>
    public async Task<PermissionScope?> GetPermissionScopeAsync(
        string userId,
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var operatorId))
        {
            return null;
        }

        // Super admin has Global scope
        if (await IsSuperAdminAsync(userId, cancellationToken))
        {
            return PermissionScope.Global;
        }

        // Get all permissions with scopes (cached)
        var permissionsWithScopes = await GetUserPermissionsWithScopesAsync(userId, cancellationToken);

        if (permissionsWithScopes.TryGetValue(permissionKey, out var scope))
        {
            return scope;
        }

        return null;
    }

    /// <summary>
    /// Get all permissions with their scopes for a user
    /// Uses caching for performance
    /// </summary>
    public async Task<Dictionary<string, PermissionScope>> GetUserPermissionsWithScopesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var operatorId))
        {
            return new Dictionary<string, PermissionScope>();
        }

        var cacheKey = $"{PermissionScopeCacheKeyPrefix}{userId}";

        // Try get from cache
        if (_cache.TryGetValue<Dictionary<string, PermissionScope>>(cacheKey, out var cachedPermissions)
            && cachedPermissions != null)
        {
            _logger.LogDebug("Cache hit for user {UserId} permission scopes", userId);
            return cachedPermissions;
        }

        // Load from database
        _logger.LogDebug("Cache miss for user {UserId} permission scopes, loading from database", userId);
        var permissionsWithScopes = await _permissionRepository.GetPermissionsWithScopesAsync(
            operatorId,
            cancellationToken);

        // Convert to PermissionScope enum
        var result = new Dictionary<string, PermissionScope>();
        foreach (var (key, scopeLevel) in permissionsWithScopes)
        {
            // Map database scope level to PermissionScope enum
            // Database: 4=Global, 3=Company, 2=Department, 1=Self
            var permissionScope = scopeLevel switch
            {
                4 => PermissionScope.Global,
                3 => PermissionScope.Company,
                2 => PermissionScope.Department,
                1 => PermissionScope.Self,
                _ => PermissionScope.Global // Default to Global for null/unknown
            };
            result[key] = permissionScope;
        }

        // Cache for future requests
        _cache.Set(cacheKey, result, CacheDuration);

        _logger.LogDebug(
            "Loaded {Count} permissions with scopes for user {UserId}",
            result.Count,
            userId);

        return result;
    }
}
