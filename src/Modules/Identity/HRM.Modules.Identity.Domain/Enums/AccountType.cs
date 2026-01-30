namespace HRM.Modules.Identity.Domain.Enums;

/// <summary>
/// Defines the type of authenticated account in the system.
///
/// This is the canonical enum for account classification.
/// Replaces the deprecated UserType enum.
///
/// This enum is INTERNAL to the Identity module.
/// Other modules should NOT reference this enum directly.
/// They receive DataScopeRule (contract) instead.
/// </summary>
public enum AccountType
{
    /// <summary>
    /// System account (internal operators, admins).
    /// Has access to system configuration and all data.
    /// </summary>
    System = 0,

    /// <summary>
    /// Employee account (HR employees).
    /// Access is scoped based on department/company/position.
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
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Account is active and can login.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Account is suspended (temporarily disabled).
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Account is deactivated (permanently disabled).
    /// </summary>
    Deactivated = 3
}
