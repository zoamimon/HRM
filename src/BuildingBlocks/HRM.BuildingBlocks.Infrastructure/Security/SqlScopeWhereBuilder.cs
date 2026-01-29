using Dapper;
using HRM.BuildingBlocks.Domain.Abstractions.Security;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Translates DataScopeRule to SQL WHERE clause
///
/// IMPORTANT: This class contains NO business logic.
/// It only translates the rule to SQL format.
/// All business logic is in IDataScopeRuleProvider.
///
/// Usage:
/// <code>
/// var rule = await ruleProvider.GetRuleAsync(context);
/// var parameters = new DynamicParameters();
/// var where = SqlScopeWhereBuilder.Build(rule, parameters);
/// var sql = $"SELECT * FROM Employees e WHERE 1=1 {where}";
/// var result = await connection.QueryAsync(sql, parameters);
/// </code>
/// </summary>
public static class SqlScopeWhereBuilder
{
    /// <summary>
    /// Build SQL WHERE clause fragment with standard column names
    /// Assumes: CompanyId, DepartmentId, PositionId, OwnerId columns
    /// </summary>
    public static string Build(DataScopeRule rule, DynamicParameters parameters)
    {
        return Build(rule, parameters, new SqlScopeColumnMapping());
    }

    /// <summary>
    /// Build SQL WHERE clause with custom column mapping
    /// </summary>
    public static string Build(DataScopeRule rule, DynamicParameters parameters, SqlScopeColumnMapping columns)
    {
        // Global access - no filtering
        if (rule.IsGlobal)
        {
            return string.Empty; // No WHERE clause needed
        }

        // Self-scoped - only own data
        if (rule.IsSelfScoped && rule.UserId.HasValue)
        {
            parameters.Add("@ScopeUserId", rule.UserId.Value);
            return $"AND {columns.OwnerColumn} = @ScopeUserId";
        }

        // Position-scoped
        if (rule.IsPositionScoped && rule.PositionIds.Count > 0)
        {
            parameters.Add("@ScopePositionIds", rule.PositionIds);
            return $"AND {columns.PositionColumn} IN @ScopePositionIds";
        }

        // Department-scoped
        if (rule.IsDepartmentScoped && rule.DepartmentIds.Count > 0)
        {
            parameters.Add("@ScopeDepartmentIds", rule.DepartmentIds);
            return $"AND {columns.DepartmentColumn} IN @ScopeDepartmentIds";
        }

        // Company-scoped
        if (rule.IsCompanyScoped && rule.CompanyIds.Count > 0)
        {
            parameters.Add("@ScopeCompanyIds", rule.CompanyIds);
            return $"AND {columns.CompanyColumn} IN @ScopeCompanyIds";
        }

        // No access
        return "AND 1 = 0";
    }

    /// <summary>
    /// Build for employee-based queries with join to EmployeeAssignments
    /// </summary>
    public static string BuildWithAssignments(
        DataScopeRule rule,
        DynamicParameters parameters,
        string employeeAlias = "e",
        string assignmentAlias = "ea")
    {
        if (rule.IsGlobal)
        {
            return string.Empty;
        }

        if (rule.IsSelfScoped && rule.EmployeeId.HasValue)
        {
            parameters.Add("@ScopeEmployeeId", rule.EmployeeId.Value);
            return $"AND {employeeAlias}.Id = @ScopeEmployeeId";
        }

        if (rule.IsPositionScoped && rule.PositionIds.Count > 0)
        {
            parameters.Add("@ScopePositionIds", rule.PositionIds);
            return $"AND {assignmentAlias}.PositionId IN @ScopePositionIds";
        }

        if (rule.IsDepartmentScoped && rule.DepartmentIds.Count > 0)
        {
            parameters.Add("@ScopeDepartmentIds", rule.DepartmentIds);
            return $"AND {assignmentAlias}.DepartmentId IN @ScopeDepartmentIds";
        }

        if (rule.IsCompanyScoped && rule.CompanyIds.Count > 0)
        {
            parameters.Add("@ScopeCompanyIds", rule.CompanyIds);
            return $"AND {assignmentAlias}.CompanyId IN @ScopeCompanyIds";
        }

        return "AND 1 = 0";
    }

    /// <summary>
    /// Build standalone WHERE clause (without leading AND)
    /// </summary>
    public static string BuildStandalone(DataScopeRule rule, DynamicParameters parameters)
    {
        if (rule.IsGlobal)
        {
            return "1 = 1";
        }

        if (rule.IsSelfScoped && rule.UserId.HasValue)
        {
            parameters.Add("@ScopeUserId", rule.UserId.Value);
            return "OwnerId = @ScopeUserId";
        }

        if (rule.IsPositionScoped && rule.PositionIds.Count > 0)
        {
            parameters.Add("@ScopePositionIds", rule.PositionIds);
            return "PositionId IN @ScopePositionIds";
        }

        if (rule.IsDepartmentScoped && rule.DepartmentIds.Count > 0)
        {
            parameters.Add("@ScopeDepartmentIds", rule.DepartmentIds);
            return "DepartmentId IN @ScopeDepartmentIds";
        }

        if (rule.IsCompanyScoped && rule.CompanyIds.Count > 0)
        {
            parameters.Add("@ScopeCompanyIds", rule.CompanyIds);
            return "CompanyId IN @ScopeCompanyIds";
        }

        return "1 = 0";
    }
}

/// <summary>
/// Column name mapping for SQL scope queries
/// Override defaults when table uses different column names
/// </summary>
public sealed class SqlScopeColumnMapping
{
    public string CompanyColumn { get; init; } = "CompanyId";
    public string DepartmentColumn { get; init; } = "DepartmentId";
    public string PositionColumn { get; init; } = "PositionId";
    public string OwnerColumn { get; init; } = "OwnerId";
    public string EmployeeColumn { get; init; } = "EmployeeId";
}
