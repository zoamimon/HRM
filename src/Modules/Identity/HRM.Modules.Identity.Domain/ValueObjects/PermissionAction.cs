namespace HRM.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a permission action within an entity
/// Actions define what operations can be performed (View, Create, Update, Delete)
/// Immutable by design
/// </summary>
public sealed class PermissionAction
{
    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete", "Approve", "Export")
    /// Standardized names for common actions, but can be custom per entity
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Display name for UI (e.g., "Xem", "Tạo mới", "Cập nhật")
    /// Supports localization
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Available scopes for this action
    /// Empty list means action doesn't use scopes (e.g., Create might not need scope)
    ///
    /// UI Behavior:
    /// - If empty: No scope selector shown
    /// - If has items: Scope selector shown (dropdown/radio buttons)
    /// </summary>
    public List<PermissionScope> Scopes { get; private set; } = new();

    /// <summary>
    /// Constraints that apply to this action
    /// Additional conditions that must be met for permission to be granted
    ///
    /// Examples:
    /// - ManagerOfTarget: Only managers can update subordinates
    /// - FieldRestriction: Hide salary fields from non-HR users
    /// - DateRange: Only edit records from last 30 days
    /// </summary>
    public List<PermissionConstraint> Constraints { get; private set; } = new();

    /// <summary>
    /// Default scope to pre-select in UI
    /// Null means no default (user must choose)
    ///
    /// Example: Department scope might be default for department managers
    /// </summary>
    public string? DefaultScope { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private PermissionAction()
    {
    }

    /// <summary>
    /// Create a new permission action
    /// </summary>
    /// <param name="name">Action name (e.g., "View")</param>
    /// <param name="displayName">Display name for UI</param>
    /// <param name="scopes">Available scopes (optional)</param>
    /// <param name="constraints">Constraints (optional)</param>
    /// <param name="defaultScope">Default scope (optional)</param>
    public PermissionAction(
        string name,
        string displayName,
        List<PermissionScope>? scopes = null,
        List<PermissionConstraint>? constraints = null,
        string? defaultScope = null)
    {
        Name = name;
        DisplayName = displayName;
        Scopes = scopes ?? new List<PermissionScope>();
        Constraints = constraints ?? new List<PermissionConstraint>();
        DefaultScope = defaultScope;
    }

    /// <summary>
    /// Factory: View action with all scopes
    /// </summary>
    public static PermissionAction View(
        string displayName = "Xem",
        List<PermissionScope>? scopes = null,
        List<PermissionConstraint>? constraints = null)
    {
        // Default scopes if not provided
        scopes ??= new List<PermissionScope>
        {
            PermissionScope.Company(),
            PermissionScope.Department(),
            PermissionScope.Position(isReadOnly: true),
            PermissionScope.Self()
        };

        return new PermissionAction("View", displayName, scopes, constraints);
    }

    /// <summary>
    /// Factory: Create action (typically no scope - creates in user's context)
    /// </summary>
    public static PermissionAction Create(
        string displayName = "Tạo mới",
        List<PermissionScope>? scopes = null,
        List<PermissionConstraint>? constraints = null)
    {
        return new PermissionAction("Create", displayName, scopes, constraints);
    }

    /// <summary>
    /// Factory: Update action with scopes
    /// </summary>
    public static PermissionAction Update(
        string displayName = "Cập nhật",
        List<PermissionScope>? scopes = null,
        List<PermissionConstraint>? constraints = null)
    {
        // Default scopes if not provided
        scopes ??= new List<PermissionScope>
        {
            PermissionScope.Company(),
            PermissionScope.Department(),
            PermissionScope.Self()
        };

        return new PermissionAction("Update", displayName, scopes, constraints);
    }

    /// <summary>
    /// Factory: Delete action with scopes
    /// </summary>
    public static PermissionAction Delete(
        string displayName = "Xóa",
        List<PermissionScope>? scopes = null,
        List<PermissionConstraint>? constraints = null)
    {
        // Default scopes if not provided
        scopes ??= new List<PermissionScope>
        {
            PermissionScope.Company(),
            PermissionScope.Department()
        };

        return new PermissionAction("Delete", displayName, scopes, constraints);
    }

    /// <summary>
    /// Factory: Approve action (workflow-specific)
    /// </summary>
    public static PermissionAction Approve(
        string displayName = "Phê duyệt",
        List<PermissionScope>? scopes = null,
        List<PermissionConstraint>? constraints = null)
    {
        return new PermissionAction("Approve", displayName, scopes, constraints);
    }

    /// <summary>
    /// Factory: Export action (typically company or department scope)
    /// </summary>
    public static PermissionAction Export(
        string displayName = "Xuất dữ liệu",
        List<PermissionScope>? scopes = null,
        List<PermissionConstraint>? constraints = null)
    {
        scopes ??= new List<PermissionScope>
        {
            PermissionScope.Company(),
            PermissionScope.Department()
        };

        return new PermissionAction("Export", displayName, scopes, constraints);
    }

    /// <summary>
    /// Check if action has any scopes
    /// </summary>
    public bool HasScopes() => Scopes.Any();

    /// <summary>
    /// Check if action has any constraints
    /// </summary>
    public bool HasConstraints() => Constraints.Any();

    /// <summary>
    /// Get scope by value
    /// </summary>
    public PermissionScope? GetScope(HRM.BuildingBlocks.Domain.Enums.ScopeLevel scopeLevel)
    {
        return Scopes.FirstOrDefault(s => s.Value == scopeLevel);
    }

    /// <summary>
    /// Check if action allows specific scope
    /// </summary>
    public bool AllowsScope(HRM.BuildingBlocks.Domain.Enums.ScopeLevel scopeLevel)
    {
        return Scopes.Any(s => s.Value == scopeLevel);
    }
}
