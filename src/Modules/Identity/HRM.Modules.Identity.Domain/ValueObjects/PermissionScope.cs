using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a permission scope option
/// Scopes define the data visibility boundary for a permission action
/// Immutable by design
/// </summary>
public sealed class PermissionScope
{
    /// <summary>
    /// Scope level (Company, Department, Position, Employee/Self)
    /// Maps to ScopeLevel enum from BuildingBlocks
    /// </summary>
    public ScopeLevel Value { get; private set; }

    /// <summary>
    /// Display name for UI (e.g., "Toàn công ty", "Cùng phòng ban")
    /// Supports localization
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Whether this scope is read-only (cannot be used for Update/Delete)
    /// Default: false (can be used for all actions)
    ///
    /// Use Cases:
    /// - Position scope might be read-only in some templates
    /// - Self scope is typically read-write for own data
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private PermissionScope()
    {
    }

    /// <summary>
    /// Create a new permission scope
    /// </summary>
    /// <param name="value">Scope level</param>
    /// <param name="displayName">Display name for UI</param>
    /// <param name="isReadOnly">Whether this scope is read-only</param>
    public PermissionScope(ScopeLevel value, string displayName, bool isReadOnly = false)
    {
        Value = value;
        DisplayName = displayName;
        IsReadOnly = isReadOnly;
    }

    /// <summary>
    /// Factory method: Global scope (Operators only)
    /// </summary>
    public static PermissionScope Global(string displayName = "Toàn hệ thống", bool isReadOnly = false)
    {
        return new PermissionScope(ScopeLevel.Global, displayName, isReadOnly);
    }

    /// <summary>
    /// Factory method: Company scope
    /// </summary>
    public static PermissionScope Company(string displayName = "Toàn công ty", bool isReadOnly = false)
    {
        return new PermissionScope(ScopeLevel.Company, displayName, isReadOnly);
    }

    /// <summary>
    /// Factory method: Department scope
    /// </summary>
    public static PermissionScope Department(string displayName = "Cùng phòng ban", bool isReadOnly = false)
    {
        return new PermissionScope(ScopeLevel.Department, displayName, isReadOnly);
    }

    /// <summary>
    /// Factory method: Position scope
    /// </summary>
    public static PermissionScope Position(string displayName = "Cùng chức danh", bool isReadOnly = false)
    {
        return new PermissionScope(ScopeLevel.Position, displayName, isReadOnly);
    }

    /// <summary>
    /// Factory method: Employee/Self scope
    /// </summary>
    public static PermissionScope Self(string displayName = "Chỉ bản thân", bool isReadOnly = false)
    {
        return new PermissionScope(ScopeLevel.Employee, displayName, isReadOnly);
    }
}
