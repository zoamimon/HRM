namespace HRM.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a permission entity (e.g., Employee, Department, Position)
/// Entities are the domain objects that actions are performed on
/// Immutable by design
/// </summary>
public sealed class PermissionEntity
{
    /// <summary>
    /// Entity name (e.g., "Employee", "Department", "LeaveRequest")
    /// Should match domain entity names
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Display name for UI (e.g., "Nhân viên", "Phòng ban")
    /// Supports localization
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Actions available for this entity
    /// Each action defines what can be done (View, Create, Update, Delete, etc.)
    /// </summary>
    public List<PermissionAction> Actions { get; private set; } = new();

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private PermissionEntity()
    {
    }

    /// <summary>
    /// Create a new permission entity
    /// </summary>
    /// <param name="name">Entity name</param>
    /// <param name="displayName">Display name for UI</param>
    /// <param name="actions">Available actions</param>
    public PermissionEntity(string name, string displayName, List<PermissionAction>? actions = null)
    {
        Name = name;
        DisplayName = displayName;
        Actions = actions ?? new List<PermissionAction>();
    }

    /// <summary>
    /// Add an action to the entity
    /// </summary>
    public void AddAction(PermissionAction action)
    {
        if (!Actions.Any(a => a.Name == action.Name))
        {
            Actions.Add(action);
        }
    }

    /// <summary>
    /// Get action by name
    /// </summary>
    public PermissionAction? GetAction(string actionName)
    {
        return Actions.FirstOrDefault(a => a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if entity has specific action
    /// </summary>
    public bool HasAction(string actionName)
    {
        return Actions.Any(a => a.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all action names
    /// </summary>
    public List<string> GetActionNames()
    {
        return Actions.Select(a => a.Name).ToList();
    }
}
