using HRM.BuildingBlocks.Domain.Abstractions.Security;
using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Application.Abstractions.Authorization;

/// <summary>
/// Context for building data scope rules
/// Contains user information needed to determine scope
/// </summary>
public sealed record DataScopeContext
{
    /// <summary>
    /// Current authenticated user ID (Account ID)
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// User's scope level for the current permission
    /// </summary>
    public required ScopeLevel ScopeLevel { get; init; }

    /// <summary>
    /// Current permission being checked (e.g., "Personnel.Employee.View")
    /// </summary>
    public required string Permission { get; init; }

    /// <summary>
    /// True if user is a system account (Operator)
    /// System accounts typically have global access
    /// </summary>
    public bool IsSystemAccount { get; init; }

    /// <summary>
    /// User's employee ID (for employee accounts)
    /// Null for system accounts
    /// </summary>
    public Guid? EmployeeId { get; init; }
}

/// <summary>
/// Single Source of Truth for data scope rules
///
/// This is where ALL scope logic lives:
/// - Company > Department > Position > Self hierarchy
/// - EmployeeAssignments lookup
/// - Role → Scope mapping
/// - Multi-tenant logic
///
/// EfScopeExpressionBuilder and SqlScopeWhereBuilder only TRANSLATE
/// the rules to their respective formats. They contain NO business logic.
///
/// Usage:
/// <code>
/// // Get rule from provider (business logic here)
/// var rule = await ruleProvider.GetRuleAsync(context);
///
/// // Translate to EF Expression (no logic, just format)
/// var expression = efBuilder.Build&lt;Employee&gt;(rule);
///
/// // Translate to SQL WHERE (no logic, just format)
/// var where = sqlBuilder.Build(rule, parameters);
/// </code>
/// </summary>
public interface IDataScopeRuleProvider
{
    /// <summary>
    /// Get the data scope rule for the current context
    /// This is the ONLY place where scope logic is implemented
    /// </summary>
    /// <param name="context">User context with scope information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DataScopeRule that can be translated to EF/SQL</returns>
    Task<DataScopeRule> GetRuleAsync(DataScopeContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get rule synchronously (for cached/in-memory scenarios)
    /// Use GetRuleAsync when database lookup is needed
    /// </summary>
    DataScopeRule GetRule(DataScopeContext context);
}
