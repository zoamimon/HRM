using Microsoft.AspNetCore.Authorization;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// Authorization requirement for permission-based access control
///
/// Design:
/// - Implements IAuthorizationRequirement for ASP.NET Core Authorization
/// - Defines required module, entity, and action for access
/// - Used with HasPermissionAttribute on endpoints
///
/// Permission Format: {Module}.{Entity}.{Action}
/// Example: "Personnel.Employee.View", "Identity.Role.Create"
///
/// Usage:
/// <code>
/// // On endpoint
/// [HasPermission("Personnel", "Employee", "View")]
/// public async Task&lt;IResult&gt; GetEmployees() { ... }
///
/// // Or with policy
/// [Authorize(Policy = "Personnel.Employee.View")]
/// </code>
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Module name (e.g., "Personnel", "Identity", "Attendance")
    /// </summary>
    public string Module { get; }

    /// <summary>
    /// Entity name (e.g., "Employee", "Role", "Timesheet")
    /// </summary>
    public string Entity { get; }

    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete")
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Full permission string: {Module}.{Entity}.{Action}
    /// </summary>
    public string Permission => $"{Module}.{Entity}.{Action}";

    public PermissionRequirement(string module, string entity, string action)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        Action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// Create requirement from permission string
    /// </summary>
    /// <param name="permission">Permission string in format {Module}.{Entity}.{Action}</param>
    public static PermissionRequirement FromPermissionString(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("Permission string cannot be null or empty", nameof(permission));
        }

        var parts = permission.Split('.');
        if (parts.Length != 3)
        {
            throw new ArgumentException(
                $"Invalid permission format: '{permission}'. Expected format: Module.Entity.Action",
                nameof(permission));
        }

        return new PermissionRequirement(parts[0], parts[1], parts[2]);
    }
}
