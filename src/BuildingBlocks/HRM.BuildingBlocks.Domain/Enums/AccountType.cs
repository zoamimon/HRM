namespace HRM.BuildingBlocks.Domain.Enums;

/// <summary>
/// Defines the type of authenticated account in the system.
///
/// This is the canonical enum for account classification.
/// Replaces the deprecated UserType enum.
///
/// Mapping from deprecated UserType:
/// - UserType.Operator → AccountType.System
/// - UserType.User → AccountType.Employee
/// </summary>
public enum AccountType
{
    /// <summary>
    /// System account (internal operators, admins).
    /// Has access to system configuration and all data.
    ///
    /// Characteristics:
    /// - Not linked to any employee
    /// - Global access (no ScopeLevel restrictions)
    /// - Can manage all data across all companies
    /// - Used for: System admins, IT support, super users
    /// </summary>
    System = 0,

    /// <summary>
    /// Employee account (HR employees).
    /// Access is scoped based on department/company/position.
    ///
    /// Characteristics:
    /// - Linked to an Employee entity via EmployeeProfile
    /// - Has ScopeLevel (Company/Department/Position/Employee)
    /// - Can only access data within their assigned scope
    /// - Used for: Regular employees, managers, executives
    /// </summary>
    Employee = 1
}

/// <summary>
/// Account status enumeration.
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Account is pending activation.
    /// Cannot login until activated.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Account is active and can login.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Account is suspended (temporarily disabled).
    /// Cannot login. Can be reactivated.
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Account is deactivated (permanently disabled).
    /// Cannot login. Typically not reactivated.
    /// </summary>
    Deactivated = 3
}
