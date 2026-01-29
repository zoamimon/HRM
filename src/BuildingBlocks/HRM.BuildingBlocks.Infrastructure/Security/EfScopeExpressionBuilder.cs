using System.Linq.Expressions;
using HRM.BuildingBlocks.Domain.Abstractions.Security;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Translates DataScopeRule to EF Core Expression
///
/// IMPORTANT: This class contains NO business logic.
/// It only translates the rule to Expression format.
/// All business logic is in IDataScopeRuleProvider.
///
/// Usage:
/// <code>
/// var rule = await ruleProvider.GetRuleAsync(context);
/// var expression = EfScopeExpressionBuilder.Build&lt;Employee&gt;(rule);
/// var filtered = query.Where(expression);
/// </code>
/// </summary>
public static class EfScopeExpressionBuilder
{
    /// <summary>
    /// Build filter expression for IScopedEntity
    /// </summary>
    public static Expression<Func<T, bool>> Build<T>(DataScopeRule rule)
        where T : class, IScopedEntity
    {
        // Global access - no filtering
        if (rule.IsGlobal)
        {
            return _ => true;
        }

        // Self-scoped - only own data
        if (rule.IsSelfScoped && rule.UserId.HasValue)
        {
            var userId = rule.UserId.Value;
            return x => x.OwnerId == userId;
        }

        // Position-scoped
        if (rule.IsPositionScoped && rule.PositionIds.Count > 0)
        {
            var positionIds = rule.PositionIds;
            return x => x.PositionId != null && positionIds.Contains(x.PositionId.Value);
        }

        // Department-scoped
        if (rule.IsDepartmentScoped && rule.DepartmentIds.Count > 0)
        {
            var departmentIds = rule.DepartmentIds;
            return x => x.DepartmentId != null && departmentIds.Contains(x.DepartmentId.Value);
        }

        // Company-scoped
        if (rule.IsCompanyScoped && rule.CompanyIds.Count > 0)
        {
            var companyIds = rule.CompanyIds;
            return x => x.CompanyId != null && companyIds.Contains(x.CompanyId.Value);
        }

        // No access
        return _ => false;
    }

    /// <summary>
    /// Build filter expression for company-scoped entities only
    /// </summary>
    public static Expression<Func<T, bool>> BuildCompanyScope<T>(DataScopeRule rule)
        where T : class, ICompanyScopedEntity
    {
        if (rule.IsGlobal)
        {
            return _ => true;
        }

        if (rule.IsCompanyScoped && rule.CompanyIds.Count > 0)
        {
            var companyIds = rule.CompanyIds;
            return x => x.CompanyId != null && companyIds.Contains(x.CompanyId.Value);
        }

        return _ => false;
    }

    /// <summary>
    /// Build filter expression for owned entities only
    /// </summary>
    public static Expression<Func<T, bool>> BuildOwnerScope<T>(DataScopeRule rule)
        where T : class, IOwnedEntity
    {
        if (rule.IsGlobal)
        {
            return _ => true;
        }

        if (rule.IsSelfScoped && rule.UserId.HasValue)
        {
            var userId = rule.UserId.Value;
            return x => x.OwnerId == userId;
        }

        return _ => false;
    }

    /// <summary>
    /// Build custom filter expression with entity-specific logic
    /// Use this when entity doesn't implement IScopedEntity
    /// </summary>
    public static Expression<Func<T, bool>> BuildCustom<T>(
        DataScopeRule rule,
        Expression<Func<T, Guid?>> companySelector,
        Expression<Func<T, Guid?>> departmentSelector,
        Expression<Func<T, Guid?>> positionSelector,
        Expression<Func<T, Guid>> ownerSelector)
        where T : class
    {
        if (rule.IsGlobal)
        {
            return _ => true;
        }

        // Build expression based on rule
        // This is more complex but allows flexibility for non-standard entities
        var parameter = Expression.Parameter(typeof(T), "x");

        if (rule.IsSelfScoped && rule.UserId.HasValue)
        {
            var ownerBody = ReplacementVisitor.Replace(
                ownerSelector.Body, ownerSelector.Parameters[0], parameter);
            var userIdConstant = Expression.Constant(rule.UserId.Value);
            var equals = Expression.Equal(ownerBody, userIdConstant);
            return Expression.Lambda<Func<T, bool>>(equals, parameter);
        }

        if (rule.IsPositionScoped && rule.PositionIds.Count > 0)
        {
            return BuildContainsExpression(parameter, positionSelector, rule.PositionIds);
        }

        if (rule.IsDepartmentScoped && rule.DepartmentIds.Count > 0)
        {
            return BuildContainsExpression(parameter, departmentSelector, rule.DepartmentIds);
        }

        if (rule.IsCompanyScoped && rule.CompanyIds.Count > 0)
        {
            return BuildContainsExpression(parameter, companySelector, rule.CompanyIds);
        }

        return _ => false;
    }

    private static Expression<Func<T, bool>> BuildContainsExpression<T>(
        ParameterExpression parameter,
        Expression<Func<T, Guid?>> selector,
        IReadOnlyList<Guid> ids)
        where T : class
    {
        var selectorBody = ReplacementVisitor.Replace(
            selector.Body, selector.Parameters[0], parameter);

        // Check for null
        var notNull = Expression.NotEqual(selectorBody, Expression.Constant(null, typeof(Guid?)));

        // Get value from nullable
        var getValue = Expression.Property(selectorBody, "Value");

        // Contains check
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(Guid));

        var idsConstant = Expression.Constant(ids);
        var contains = Expression.Call(containsMethod, idsConstant, getValue);

        // Combine: x.PropertyId != null && ids.Contains(x.PropertyId.Value)
        var combined = Expression.AndAlso(notNull, contains);

        return Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    /// <summary>
    /// Helper to replace parameter in expression
    /// </summary>
    private sealed class ReplacementVisitor : ExpressionVisitor
    {
        private readonly Expression _oldValue;
        private readonly Expression _newValue;

        private ReplacementVisitor(Expression oldValue, Expression newValue)
        {
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public static Expression Replace(Expression expression, Expression oldValue, Expression newValue)
        {
            return new ReplacementVisitor(oldValue, newValue).Visit(expression);
        }

        public override Expression? Visit(Expression? node)
        {
            if (node == _oldValue)
                return _newValue;
            return base.Visit(node);
        }
    }
}
