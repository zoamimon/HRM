using Dapper;
using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Application.Abstractions.Data;
using HRM.BuildingBlocks.Domain.Enums;
using System.Data;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// [DEPRECATED] Implementation of IDataScopingService
///
/// IMPORTANT: This class is deprecated. Use the Scope Specification Pattern instead:
/// - IDataScopeRuleProvider: Single source of truth for scope rules
/// - SqlScopeWhereBuilder: Translates rules to SQL WHERE clauses
///
/// Migration:
/// 1. Inject IDataScopeRuleProvider
/// 2. Get rule: var rule = await ruleProvider.GetRuleAsync(context)
/// 3. Build SQL: var where = SqlScopeWhereBuilder.Build(rule, parameters)
/// </summary>
[Obsolete("Use IDataScopeRuleProvider + SqlScopeWhereBuilder instead. See Scope Specification Pattern.")]
public sealed class DataScopingService : IDataScopingService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDbConnection _connection;
    private DataScopeContext? _cachedScopeContext;

    /// <summary>
    /// Constructor with dependencies
    /// </summary>
    /// <param name="currentUserService">Service to get current user information</param>
    /// <param name="connection">Database connection for querying assignments</param>
    public DataScopingService(
        ICurrentUserService currentUserService,
        IDbConnection connection)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Get current user's data scope context
    /// Loads active assignments and builds allowed IDs
    ///
    /// Caching:
    /// - Cached in _cachedScopeContext for request lifetime
    /// - Subsequent calls return cached context
    /// - Service is scoped (per request), cache cleared between requests
    /// </summary>
    public async Task<DataScopeContext> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        // Return cached context if available
        if (_cachedScopeContext is not null)
        {
            return _cachedScopeContext;
        }

        // Build scope context based on user type
        if (!_currentUserService.IsAuthenticated)
        {
            throw new InvalidOperationException(
                "User is not authenticated. Cannot determine data scope."
            );
        }

        if (_currentUserService.IsOperator())
        {
            // Operators have global access (no scoping)
            _cachedScopeContext = new DataScopeContext
            {
                UserType = UserType.Operator,
                UserId = _currentUserService.UserId,
                ScopeLevel = null,
                AllowedCompanyIds = new List<Guid>(),
                AllowedDepartmentIds = new List<Guid>(),
                AllowedPositionIds = new List<Guid>()
            };

            return _cachedScopeContext;
        }

        // User: Load assignments from database
        var employeeId = _currentUserService.EmployeeId;
        if (employeeId is null)
        {
            throw new InvalidOperationException(
                "User type is User but EmployeeId is null. Invalid authentication state."
            );
        }

        var scopeLevel = _currentUserService.ScopeLevel ?? ScopeLevel.Employee;

        // Query active assignments
        var assignments = await LoadActiveAssignmentsAsync(employeeId.Value, cancellationToken);

        // Build allowed IDs based on scope level
        var context = new DataScopeContext
        {
            UserType = UserType.User,
            UserId = _currentUserService.UserId,
            ScopeLevel = scopeLevel,
            AllowedCompanyIds = assignments.Select(a => a.CompanyId).Distinct().ToList(),
            AllowedDepartmentIds = assignments.Select(a => a.DepartmentId).Distinct().ToList(),
            AllowedPositionIds = assignments.Select(a => a.PositionId).Distinct().ToList()
        };

        _cachedScopeContext = context;
        return _cachedScopeContext;
    }

    /// <summary>
    /// Build SQL WHERE clause filter for data scoping
    ///
    /// Returns:
    /// - Empty string for Operators (no filtering)
    /// - SQL filter with parameters for Users
    ///
    /// Example Output:
    /// - "AND ea.CompanyId IN @AllowedCompanyIds"
    /// - "AND ea.DepartmentId IN @AllowedDepartmentIds"
    /// - "AND e.Id = @CurrentUserId"
    /// </summary>
    public string BuildScopeFilter(DataScopeContext scopeContext, dynamic parameters)
    {
        if (scopeContext is null)
        {
            throw new ArgumentNullException(nameof(scopeContext));
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        // Operators: No filtering
        if (scopeContext.IsOperator)
        {
            return string.Empty;
        }

        // Users: Apply filtering based on scope level
        return scopeContext.ScopeLevel switch
        {
            ScopeLevel.Company => BuildCompanyFilter(scopeContext, parameters),
            ScopeLevel.Department => BuildDepartmentFilter(scopeContext, parameters),
            ScopeLevel.Position => BuildPositionFilter(scopeContext, parameters),
            ScopeLevel.Employee => BuildEmployeeFilter(scopeContext, parameters),
            _ => throw new InvalidOperationException($"Unknown scope level: {scopeContext.ScopeLevel}")
        };
    }

    /// <summary>
    /// Check if current user can access specific employee
    ///
    /// Validation:
    /// - Operators: Always true (global access)
    /// - Users: Query database to check if employee in scope
    /// </summary>
    public async Task<bool> CanAccessEmployeeAsync(
        DataScopeContext scopeContext,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (scopeContext is null)
        {
            throw new ArgumentNullException(nameof(scopeContext));
        }

        // Operators can access all employees
        if (scopeContext.IsOperator)
        {
            return true;
        }

        // Employee-level: Can only access own data
        if (scopeContext.ScopeLevel == ScopeLevel.Employee)
        {
            return employeeId == scopeContext.UserId;
        }

        // For higher levels: Check if employee in allowed scope
        var sql = scopeContext.ScopeLevel switch
        {
            ScopeLevel.Company => @"
                SELECT COUNT(1)
                FROM personnel.EmployeeAssignments ea
                WHERE ea.EmployeeId = @EmployeeId
                    AND ea.CompanyId IN @AllowedCompanyIds
                    AND (ea.EndDate IS NULL OR ea.EndDate > GETUTCDATE())",

            ScopeLevel.Department => @"
                SELECT COUNT(1)
                FROM personnel.EmployeeAssignments ea
                WHERE ea.EmployeeId = @EmployeeId
                    AND ea.DepartmentId IN @AllowedDepartmentIds
                    AND (ea.EndDate IS NULL OR ea.EndDate > GETUTCDATE())",

            ScopeLevel.Position => @"
                SELECT COUNT(1)
                FROM personnel.EmployeeAssignments ea
                WHERE ea.EmployeeId = @EmployeeId
                    AND ea.PositionId IN @AllowedPositionIds
                    AND (ea.EndDate IS NULL OR ea.EndDate > GETUTCDATE())",

            _ => throw new InvalidOperationException($"Unknown scope level: {scopeContext.ScopeLevel}")
        };

        var parameters = new
        {
            EmployeeId = employeeId,
            AllowedCompanyIds = scopeContext.AllowedCompanyIds,
            AllowedDepartmentIds = scopeContext.AllowedDepartmentIds,
            AllowedPositionIds = scopeContext.AllowedPositionIds
        };

        var count = await _connection.ExecuteScalarAsync<int>(sql, parameters);
        return count > 0;
    }

    // Private helper methods

    private async Task<List<AssignmentDto>> LoadActiveAssignmentsAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT
                ea.CompanyId,
                ea.DepartmentId,
                ea.PositionId
            FROM personnel.EmployeeAssignments ea
            WHERE ea.EmployeeId = @EmployeeId
                AND (ea.EndDate IS NULL OR ea.EndDate > GETUTCDATE())";

        var assignments = await _connection.QueryAsync<AssignmentDto>(
            sql,
            new { EmployeeId = employeeId }
        );

        return assignments.ToList();
    }

    private static string BuildCompanyFilter(DataScopeContext context, dynamic parameters)
    {
        if (!context.AllowedCompanyIds.Any())
        {
            // No companies assigned = no access
            return "AND 1 = 0";
        }

        parameters.AllowedCompanyIds = context.AllowedCompanyIds;
        return "AND ea.CompanyId IN @AllowedCompanyIds";
    }

    private static string BuildDepartmentFilter(DataScopeContext context, dynamic parameters)
    {
        if (!context.AllowedDepartmentIds.Any())
        {
            // No departments assigned = no access
            return "AND 1 = 0";
        }

        parameters.AllowedDepartmentIds = context.AllowedDepartmentIds;
        return "AND ea.DepartmentId IN @AllowedDepartmentIds";
    }

    private static string BuildPositionFilter(DataScopeContext context, dynamic parameters)
    {
        if (!context.AllowedPositionIds.Any())
        {
            // No positions assigned = no access
            return "AND 1 = 0";
        }

        parameters.AllowedPositionIds = context.AllowedPositionIds;
        return "AND ea.PositionId IN @AllowedPositionIds";
    }

    private static string BuildEmployeeFilter(DataScopeContext context, dynamic parameters)
    {
        parameters.CurrentUserId = context.UserId;
        return "AND e.Id = @CurrentUserId";
    }

    // DTO for loading assignments
    private sealed class AssignmentDto
    {
        public Guid CompanyId { get; init; }
        public Guid DepartmentId { get; init; }
        public Guid PositionId { get; init; }
    }
}
