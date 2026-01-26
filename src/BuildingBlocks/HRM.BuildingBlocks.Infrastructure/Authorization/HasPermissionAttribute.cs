using Microsoft.AspNetCore.Authorization;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// Authorization attribute for permission-based access control
///
/// Design:
/// - Wraps [Authorize(Policy = "...")] for cleaner syntax
/// - Generates policy name from module, entity, action
/// - Used on controllers or endpoints
///
/// Policy Naming Convention:
/// - Format: {Module}.{Entity}.{Action}
/// - Example: "Personnel.Employee.View"
/// - Policies are registered dynamically via PermissionPolicyProvider
///
/// Usage:
/// <code>
/// // On controller
/// [HasPermission("Personnel", "Employee", "View")]
/// public class EmployeeController : ControllerBase { }
///
/// // On action
/// [HasPermission("Personnel", "Employee", "Create")]
/// public async Task&lt;IActionResult&gt; Create([FromBody] CreateEmployeeRequest request) { }
///
/// // On minimal API endpoint
/// app.MapGet("/employees", GetEmployees)
///    .RequireAuthorization(new HasPermissionAttribute("Personnel", "Employee", "View"));
/// </code>
/// </summary>
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
