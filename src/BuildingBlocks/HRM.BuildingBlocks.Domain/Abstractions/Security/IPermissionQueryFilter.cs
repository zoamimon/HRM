using System.Linq.Expressions;
using HRM.BuildingBlocks.Domain.Enums;

namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// [DEPRECATED] Interface for permission-based query filters
///
/// IMPORTANT: This interface is deprecated. Use the Scope Specification Pattern instead:
/// - IDataScopeRuleProvider: Single source of truth for scope rules
/// - EfScopeExpressionBuilder: Translates rules to EF expressions
/// - SqlScopeWhereBuilder: Translates rules to SQL WHERE clauses
///
/// Migration:
/// 1. Inject IDataScopeRuleProvider
/// 2. Get rule: var rule = await ruleProvider.GetRuleAsync(context)
/// 3. Build expression: var expr = EfScopeExpressionBuilder.Build&lt;T&gt;(rule)
/// </summary>
[Obsolete("Use IDataScopeRuleProvider + EfScopeExpressionBuilder instead. See Scope Specification Pattern.")]
public interface IPermissionQueryFilter<TEntity> where TEntity : class
{
    /// <summary>
    /// Permission key this filter applies to (e.g., "Identity.Operator.View")
    /// </summary>
    string Permission { get; }

    /// <summary>
    /// Build the filter expression based on user context
    /// </summary>
    /// <param name="context">User context with permission and scope</param>
    /// <returns>Expression to filter entities</returns>
    Expression<Func<TEntity, bool>> Build(PermissionFilterContext context);
}

/// <summary>
/// [DEPRECATED] Context for building permission query filters
/// Use DataScopeContext from IDataScopeRuleProvider instead.
/// </summary>
[Obsolete("Use DataScopeContext from IDataScopeRuleProvider instead.")]
public sealed record PermissionFilterContext
{
    /// <summary>
    /// Current user ID
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Current permission being checked
    /// </summary>
    public required string Permission { get; init; }

    /// <summary>
    /// User's scope for this permission
    /// </summary>
    public required ScopeLevel Scope { get; init; }

    /// <summary>
    /// User's department ID (for Department scope filtering)
    /// </summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>
    /// User's company ID (for Company scope filtering)
    /// </summary>
    public Guid? CompanyId { get; init; }

    /// <summary>
    /// Additional context data (for custom filters)
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; init; }
}
