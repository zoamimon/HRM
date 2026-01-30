using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a permission assigned to a role
///
/// Structure:
/// - Module.Entity.Action format (e.g., "Personnel.Employee.View")
/// - Optional Scope for data visibility (Company, Department, Position, Self)
/// - Immutable once created
///
/// Validation:
/// - Module, Entity, Action are required
/// - Scope is optional (for actions without scopes or operator roles)
/// - Format must match catalog structure
///
/// Examples:
/// - Personnel.Employee.View with Department scope
/// - Attendance.Timesheet.Approve with Company scope
/// - System.Configuration.Update (no scope - operator only)
///
/// Comparison:
/// Two RolePermissions are equal if they have the same Module, Entity, Action, and Scope.
/// This is used for duplicate detection (fail-fast validation).
/// </summary>
public sealed record RolePermission
{
    /// <summary>
    /// Module name from Permission Catalog (e.g., "Personnel", "Attendance")
    /// </summary>
    public string Module { get; }

    /// <summary>
    /// Entity name from Permission Catalog (e.g., "Employee", "Timesheet")
    /// </summary>
    public string Entity { get; }

    /// <summary>
    /// Action name from Permission Catalog (e.g., "View", "Create", "Approve")
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Optional scope for data visibility
    /// NULL = No scope restriction (for operators or actions without scopes)
    ///
    /// User scopes:
    /// - Company: All data in assigned companies
    /// - Department: All data in assigned departments
    /// - Position: Team members with same position
    /// - Self: Only own data
    /// </summary>
    public ScopeLevel? Scope { get; }

    /// <summary>
    /// Full permission identifier in format "Module.Entity.Action"
    /// Used for permission checks and display
    /// Example: "Personnel.Employee.View"
    /// </summary>
    public string PermissionKey => $"{Module}.{Entity}.{Action}";

    /// <summary>
    /// Private constructor for creating RolePermission instances
    /// Use static factory methods for validation
    /// </summary>
    private RolePermission(string module, string entity, string action, ScopeLevel? scope)
    {
        Module = module;
        Entity = entity;
        Action = action;
        Scope = scope;
    }

    /// <summary>
    /// Factory method to create a RolePermission with validation
    ///
    /// Validation Rules:
    /// - Module, Entity, Action cannot be null or empty
    /// - Names should match catalog structure (validated at application layer)
    ///
    /// Fail-fast Strategy:
    /// - Throws ArgumentException immediately if validation fails
    /// - No silent failures or default values
    /// </summary>
    /// <param name="module">Module name from catalog</param>
    /// <param name="entity">Entity name from catalog</param>
    /// <param name="action">Action name from catalog</param>
    /// <param name="scope">Optional scope level</param>
    /// <returns>Valid RolePermission instance</returns>
    /// <exception cref="ArgumentException">If module, entity, or action is null/empty</exception>
    public static RolePermission Create(string module, string entity, string action, ScopeLevel? scope = null)
    {
        if (string.IsNullOrWhiteSpace(module))
            throw new ArgumentException("Module cannot be null or empty", nameof(module));

        if (string.IsNullOrWhiteSpace(entity))
            throw new ArgumentException("Entity cannot be null or empty", nameof(entity));

        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be null or empty", nameof(action));

        return new RolePermission(module, entity, action, scope);
    }

    /// <summary>
    /// Check if this permission has a scope restriction
    /// </summary>
    public bool HasScope() => Scope.HasValue;

    /// <summary>
    /// Get scope display name for UI
    /// </summary>
    public string GetScopeDisplay() => Scope switch
    {
        ScopeLevel.Company => "Company",
        ScopeLevel.Department => "Department",
        ScopeLevel.Position => "Position",
        ScopeLevel.Employee => "Self",
        null => "No Scope",
        _ => "Unknown"
    };

    /// <summary>
    /// Format permission for display: "Module.Entity.Action (Scope)"
    /// Example: "Personnel.Employee.View (Department)"
    /// </summary>
    public override string ToString()
    {
        return HasScope() ? $"{PermissionKey} ({GetScopeDisplay()})" : PermissionKey;
    }
}
