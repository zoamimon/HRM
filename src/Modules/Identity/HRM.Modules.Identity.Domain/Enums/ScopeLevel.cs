namespace HRM.Modules.Identity.Domain.Enums;

/// <summary>
/// Defines the data visibility scope for authorization and data filtering.
///
/// This enum is INTERNAL to the Identity module (Authorization vocabulary).
/// Other modules should NOT reference this enum directly.
/// They receive DataScopeRule (contract) instead.
///
/// Hierarchy (lower number = wider access):
/// - Global (0): System-wide access (System accounts only)
/// - Company (1): Company-wide access
/// - Department (2): Department-level access
/// - Position (3): Position/Team-level access
/// - Employee (4): Self-only access
/// </summary>
public enum ScopeLevel
{
    /// <summary>
    /// Global/System-wide access (System accounts only).
    /// Can view and manage all data across all companies.
    /// </summary>
    Global = 0,

    /// <summary>
    /// Company-level access.
    /// User can view and manage data within their assigned companies.
    /// </summary>
    Company = 1,

    /// <summary>
    /// Department-level access.
    /// User can view and manage data within their assigned departments.
    /// </summary>
    Department = 2,

    /// <summary>
    /// Position-level access (Team level).
    /// User can view and manage data for employees with the same position.
    /// </summary>
    Position = 3,

    /// <summary>
    /// Employee-level access (Self-service only).
    /// User can only view and manage their own data.
    /// This is the default and most restrictive level.
    /// </summary>
    Employee = 4
}
