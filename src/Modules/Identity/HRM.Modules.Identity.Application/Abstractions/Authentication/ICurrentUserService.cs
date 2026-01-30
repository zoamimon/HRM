using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Application.Abstractions.Authentication;

/// <summary>
/// Identity module's typed user context service.
/// Extends IExecutionContext with Identity-specific business vocabulary.
///
/// Design (kgrzybek-style Module Isolation):
/// - IExecutionContext (BuildingBlocks): primitive types only (Guid, string, bool)
/// - ICurrentUserService (Identity): adds AccountType, ScopeLevel, EmployeeId
/// - Other modules depend on IExecutionContext, NOT ICurrentUserService
/// - Only Identity module knows about AccountType and ScopeLevel
///
/// Usage in Identity module:
/// <code>
/// public class SomeIdentityHandler
/// {
///     private readonly ICurrentUserService _currentUser;
///
///     public async Task Handle(...)
///     {
///         if (_currentUser.IsSystemAccount())
///         {
///             // System accounts have global access
///         }
///         else if (_currentUser.ScopeLevel == ScopeLevel.Department)
///         {
///             // Department-level scoping
///         }
///     }
/// }
/// </code>
/// </summary>
public interface ICurrentUserService : IExecutionContext
{
    /// <summary>
    /// Gets the current user's account type (System or Employee).
    /// </summary>
    AccountType AccountType { get; }

    /// <summary>
    /// Gets the current user's type (deprecated - use AccountType).
    /// </summary>
    [Obsolete("Use AccountType property instead")]
#pragma warning disable CS0618
    UserType UserType { get; }
#pragma warning restore CS0618

    /// <summary>
    /// Gets the current user's scope level (only for Employee accounts).
    /// Null for System accounts (they have global access).
    /// </summary>
    ScopeLevel? ScopeLevel { get; }

    /// <summary>
    /// Gets the current user's employee ID (only for Employee accounts).
    /// Null for System accounts.
    /// </summary>
    Guid? EmployeeId { get; }

    /// <summary>
    /// Checks if the current user is a System account.
    /// </summary>
    bool IsSystemAccount();

    /// <summary>
    /// Checks if the current user is an Employee account.
    /// </summary>
    bool IsEmployeeAccount();

    /// <summary>
    /// Checks if the current user is an Operator (deprecated - use IsSystemAccount).
    /// </summary>
    [Obsolete("Use IsSystemAccount() instead")]
    bool IsOperator();

    /// <summary>
    /// Checks if the current user is a User (deprecated - use IsEmployeeAccount).
    /// </summary>
    [Obsolete("Use IsEmployeeAccount() instead")]
    bool IsUser();
}
