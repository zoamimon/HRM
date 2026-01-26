namespace HRM.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing a permission module (e.g., Personnel, Attendance, Payroll)
/// Modules are high-level groupings of related entities
/// Immutable by design
/// </summary>
public sealed class PermissionModule
{
    /// <summary>
    /// Module name (e.g., "Personnel", "Attendance", "Payroll")
    /// Should match module names in the system
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Display name for UI (e.g., "Quản lý nhân sự", "Chấm công")
    /// Supports localization
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Entities within this module
    /// Each entity represents a domain object (Employee, Department, etc.)
    /// </summary>
    public List<PermissionEntity> Entities { get; private set; } = new();

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private PermissionModule()
    {
    }

    /// <summary>
    /// Create a new permission module
    /// </summary>
    /// <param name="name">Module name</param>
    /// <param name="displayName">Display name for UI</param>
    /// <param name="entities">Entities within this module</param>
    public PermissionModule(string name, string displayName, List<PermissionEntity>? entities = null)
    {
        Name = name;
        DisplayName = displayName;
        Entities = entities ?? new List<PermissionEntity>();
    }

    /// <summary>
    /// Add an entity to the module
    /// </summary>
    public void AddEntity(PermissionEntity entity)
    {
        if (!Entities.Any(e => e.Name == entity.Name))
        {
            Entities.Add(entity);
        }
    }

    /// <summary>
    /// Get entity by name
    /// </summary>
    public PermissionEntity? GetEntity(string entityName)
    {
        return Entities.FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if module has specific entity
    /// </summary>
    public bool HasEntity(string entityName)
    {
        return Entities.Any(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all entity names
    /// </summary>
    public List<string> GetEntityNames()
    {
        return Entities.Select(e => e.Name).ToList();
    }
}
