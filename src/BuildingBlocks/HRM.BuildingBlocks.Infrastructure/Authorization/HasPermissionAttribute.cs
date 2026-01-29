using Microsoft.AspNetCore.Authorization;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// [DEPRECATED] Authorization attribute for permission-based access control
///
/// IMPORTANT: This attribute is deprecated. Use RouteSecurityMap.xml instead.
/// RouteSecurityMap is the Single Source of Truth for endpoint authorization.
///
/// Migration:
/// - Remove [HasPermission] attributes from endpoints
/// - Add route to RouteSecurityMap.xml with Permission and MinScope
/// - RoutePermissionMiddleware handles authorization automatically
///
/// Old Design (deprecated):
/// - Wraps [Authorize(Policy = "...")] for cleaner syntax
/// - Generates policy name from module, entity, action
/// - Used on controllers or endpoints
/// </summary>
[Obsolete("Use RouteSecurityMap.xml instead. RoutePermissionMiddleware is the Single Source of Truth for endpoint authorization.")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Policy name prefix for permission policies
    /// </summary>
    public const string PolicyPrefix = "Permission:";

    /// <summary>
    /// Module name (e.g., "Personnel", "Identity")
    /// </summary>
    public string Module { get; }

    /// <summary>
    /// Entity name (e.g., "Employee", "Role")
    /// </summary>
    public string Entity { get; }

    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete")
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Create permission attribute
    /// </summary>
    /// <param name="module">Module name</param>
    /// <param name="entity">Entity name</param>
    /// <param name="action">Action name</param>
    public HasPermissionAttribute(string module, string entity, string action)
        : base(policy: $"{PolicyPrefix}{module}.{entity}.{action}")
    {
        Module = module;
        Entity = entity;
        Action = action;
    }

    /// <summary>
    /// Create permission attribute from full permission string
    /// </summary>
    /// <param name="permission">Permission in format Module.Entity.Action</param>
    public HasPermissionAttribute(string permission)
        : base(policy: $"{PolicyPrefix}{permission}")
    {
        var parts = permission.Split('.');
        if (parts.Length != 3)
        {
            throw new ArgumentException(
                $"Invalid permission format: '{permission}'. Expected: Module.Entity.Action",
                nameof(permission));
        }

        Module = parts[0];
        Entity = parts[1];
        Action = parts[2];
    }
}
