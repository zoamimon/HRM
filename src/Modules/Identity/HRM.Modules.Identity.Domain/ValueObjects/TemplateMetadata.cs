using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Value object representing permission template metadata
/// Contains descriptive information about the template
/// Immutable by design
/// </summary>
public sealed class TemplateMetadata
{
    /// <summary>
    /// Template name (unique identifier)
    /// Example: "HRManager", "DepartmentManager", "Employee"
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Display name for UI
    /// Example: "Quản lý nhân sự", "Trưởng phòng"
    /// Supports localization
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Template description
    /// Explains what this template is for and who should use it
    /// </summary>
    public string Description { get; private set; } = default!;

    /// <summary>
    /// Template version (for tracking changes over time)
    /// Format: "1.0", "1.1", "2.0", etc.
    /// </summary>
    public string Version { get; private set; } = default!;

    /// <summary>
    /// Who can this template be assigned to (User, Operator, or Both)
    /// Controls UI visibility and assignment logic
    /// </summary>
    public ApplicableTo ApplicableTo { get; private set; }

    /// <summary>
    /// Template category for grouping in UI
    /// Example: "Management", "HR", "Technical", "Administrative"
    /// </summary>
    public string? Category { get; private set; }

    /// <summary>
    /// Whether this is a system template (cannot be deleted/modified)
    /// System templates are built-in and protected
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private TemplateMetadata()
    {
    }

    /// <summary>
    /// Create new template metadata
    /// </summary>
    /// <param name="name">Template name (unique)</param>
    /// <param name="displayName">Display name for UI</param>
    /// <param name="description">Template description</param>
    /// <param name="version">Template version</param>
    /// <param name="applicableTo">Who can use this template</param>
    /// <param name="category">Optional category for grouping</param>
    /// <param name="isSystem">Whether this is a system template</param>
    public TemplateMetadata(
        string name,
        string displayName,
        string description,
        string version,
        ApplicableTo applicableTo,
        string? category = null,
        bool isSystem = false)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
        Version = version;
        ApplicableTo = applicableTo;
        Category = category;
        IsSystem = isSystem;
    }

    /// <summary>
    /// Check if template can be assigned to users
    /// </summary>
    public bool CanAssignToUser() => ApplicableTo == ApplicableTo.User || ApplicableTo == ApplicableTo.Both;

    /// <summary>
    /// Check if template can be assigned to operators
    /// </summary>
    public bool CanAssignToOperator() => ApplicableTo == ApplicableTo.Operator || ApplicableTo == ApplicableTo.Both;
}
