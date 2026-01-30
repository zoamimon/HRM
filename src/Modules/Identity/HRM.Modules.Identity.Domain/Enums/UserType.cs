namespace HRM.Modules.Identity.Domain.Enums;

/// <summary>
/// Defines the type of authenticated user in the system.
///
/// DEPRECATED: Use AccountType instead.
/// This enum is kept for backward compatibility during migration.
///
/// Migration Guide:
/// - UserType.Operator → AccountType.System
/// - UserType.User → AccountType.Employee
/// </summary>
[Obsolete("Use AccountType instead. UserType will be removed in a future version. " +
          "Use UserTypeExtensions.ToAccountType() for conversion.")]
public enum UserType
{
    /// <summary>
    /// System operator with global access.
    /// Maps to AccountType.System.
    /// </summary>
    [Obsolete("Use AccountType.System instead")]
    Operator = 1,

    /// <summary>
    /// Employee user with scoped access.
    /// Maps to AccountType.Employee.
    /// </summary>
    [Obsolete("Use AccountType.Employee instead")]
    User = 2
}

/// <summary>
/// Extension methods for UserType - AccountType conversion.
/// </summary>
public static class UserTypeExtensions
{
#pragma warning disable CS0618 // Type or member is obsolete
    /// <summary>
    /// Convert UserType to AccountType.
    /// </summary>
    public static AccountType ToAccountType(this UserType userType)
    {
        return userType switch
        {
            UserType.Operator => AccountType.System,
            UserType.User => AccountType.Employee,
            _ => throw new ArgumentOutOfRangeException(nameof(userType), userType, "Unknown UserType")
        };
    }

    /// <summary>
    /// Convert AccountType to UserType (for backward compatibility).
    /// </summary>
    public static UserType ToUserType(this AccountType accountType)
    {
        return accountType switch
        {
            AccountType.System => UserType.Operator,
            AccountType.Employee => UserType.User,
            _ => throw new ArgumentOutOfRangeException(nameof(accountType), accountType, "Unknown AccountType")
        };
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
