namespace HRM.BuildingBlocks.Domain.Abstractions.Security;

/// <summary>
/// Pure business model representing data scope rules
/// NO EF, NO SQL - just business logic representation
///
/// This is the Single Source of Truth for scope decisions.
/// EfScopeExpressionBuilder and SqlScopeWhereBuilder only translate this to their formats.
///
/// Hierarchy (from widest to narrowest):
/// Global > Company > Department > Position > Self
///
/// Usage:
/// <code>
/// var rule = ruleProvider.GetRule(context);
///
/// // For EF Core
/// var expression = efBuilder.Build&lt;Employee&gt;(rule);
/// query.Where(expression);
///
/// // For Dapper/SQL
/// var where = sqlBuilder.Build(rule, parameters);
/// sql += where;
/// </code>
/// </summary>
public sealed class DataScopeRule
{
    /// <summary>
    /// User has global access (no filtering)
    /// Typically for super admin or system accounts
    /// </summary>
    public bool IsGlobal { get; init; }

    /// <summary>
    /// User is scoped to specific companies
    /// </summary>
    public bool IsCompanyScoped { get; init; }

    /// <summary>
    /// User is scoped to specific departments
    /// </summary>
    public bool IsDepartmentScoped { get; init; }

    /// <summary>
    /// User is scoped to specific positions
    /// </summary>
    public bool IsPositionScoped { get; init; }

    /// <summary>
    /// User can only see their own data
    /// </summary>
    public bool IsSelfScoped { get; init; }

    /// <summary>
    /// Allowed company IDs (for company scope)
    /// </summary>
    public IReadOnlyList<Guid> CompanyIds { get; init; } = [];

    /// <summary>
    /// Allowed department IDs (for department scope)
    /// </summary>
    public IReadOnlyList<Guid> DepartmentIds { get; init; } = [];

    /// <summary>
    /// Allowed position IDs (for position scope)
    /// </summary>
    public IReadOnlyList<Guid> PositionIds { get; init; } = [];

    /// <summary>
    /// Current user ID (for self scope)
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Current user's employee ID (for employee-based filtering)
    /// </summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>
    /// Create a global access rule (no filtering)
    /// </summary>
    public static DataScopeRule Global() => new() { IsGlobal = true };

    /// <summary>
    /// Create a deny-all rule (no access)
    /// </summary>
    public static DataScopeRule None() => new();

    /// <summary>
    /// Create a company-scoped rule
    /// </summary>
    public static DataScopeRule ForCompanies(IEnumerable<Guid> companyIds, Guid userId) => new()
    {
        IsCompanyScoped = true,
        CompanyIds = companyIds.ToList(),
        UserId = userId
    };

    /// <summary>
    /// Create a department-scoped rule
    /// </summary>
    public static DataScopeRule ForDepartments(IEnumerable<Guid> departmentIds, Guid userId) => new()
    {
        IsDepartmentScoped = true,
        DepartmentIds = departmentIds.ToList(),
        UserId = userId
    };

    /// <summary>
    /// Create a position-scoped rule
    /// </summary>
    public static DataScopeRule ForPositions(IEnumerable<Guid> positionIds, Guid userId) => new()
    {
        IsPositionScoped = true,
        PositionIds = positionIds.ToList(),
        UserId = userId
    };

    /// <summary>
    /// Create a self-only rule
    /// </summary>
    public static DataScopeRule ForSelf(Guid userId, Guid? employeeId = null) => new()
    {
        IsSelfScoped = true,
        UserId = userId,
        EmployeeId = employeeId
    };

    /// <summary>
    /// Check if this rule allows any access
    /// </summary>
    public bool HasAccess => IsGlobal || IsCompanyScoped || IsDepartmentScoped || IsPositionScoped || IsSelfScoped;
}
