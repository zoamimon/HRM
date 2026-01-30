using System.Linq.Expressions;

namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// [DEPRECATED] Interface for permission-based query filters
///
/// IMPORTANT: This interface is deprecated. Use the Scope Specification Pattern instead:
/// - IDataScopeService: Resolves DataScopeRule for user + permission
/// - EfScopeExpressionBuilder: Translates rules to EF expressions
/// - SqlScopeWhereBuilder: Translates rules to SQL WHERE clauses
///
/// Migration:
/// 1. Inject IDataScopeService (business module)
/// 2. Get rule: var rule = await dataScopeService.GetScopeRuleAsync(userId, permission)
/// 3. Build expression: var expr = EfScopeExpressionBuilder.Build&lt;T&gt;(rule)
/// </summary>
[Obsolete("Use IDataScopeService + EfScopeExpressionBuilder instead. See Scope Specification Pattern.")]
public interface IPermissionQueryFilter<TEntity> where TEntity : class
{
    /// <summary>
    /// Permission key this filter applies to (e.g., "Identity.Operator.View")
    /// </summary>
    string Permission { get; }

    /// <summary>
    /// Build the filter expression based on user context
    /// </summary>
    /// <param name="context">User context with permission and scope rule</param>
    /// <returns>Expression to filter entities</returns>
    Expression<Func<TEntity, bool>> Build(PermissionFilterContext context);
}

/// <summary>
/// [DEPRECATED] Context for building permission query filters.
/// Use DataScopeRule from IDataScopeService instead.
///
/// Design: ScopeLevel removed — scope is now expressed via DataScopeRule
/// which contains the resolved scope (allowed IDs, scope type flags).
/// </summary>
[Obsolete("Use DataScopeRule from IDataScopeService instead.")]
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
    /// Resolved data scope rule for this user + permission.
    /// Contains scope type flags and allowed entity IDs.
    /// </summary>
    public required DataScopeRule ScopeRule { get; init; }

    /// <summary>
    /// Additional context data (for custom filters)
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; init; }
}
