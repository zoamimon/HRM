namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// Represents a permission scope level
/// Higher level = more access
///
/// Hierarchy:
/// Global (4) > Company (3) > Department (2) > Self (1)
///
/// Usage:
/// - A user with Company scope can access Department and Self data
/// - A user with Self scope can only access their own data
/// </summary>
public enum PermissionScope
{
    /// <summary>
    /// Only own data (Level 1)
    /// </summary>
    Self = 1,

    /// <summary>
    /// Same department only (Level 2)
    /// </summary>
    Department = 2,

    /// <summary>
    /// Whole company (Level 3)
    /// </summary>
    Company = 3,

    /// <summary>
    /// System-wide access (Level 4)
    /// Super admin level
    /// </summary>
    Global = 4
}
