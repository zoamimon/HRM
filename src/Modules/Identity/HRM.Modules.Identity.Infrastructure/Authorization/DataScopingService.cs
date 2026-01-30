using Dapper;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Application.Abstractions.Data;
using HRM.Modules.Identity.Domain.Enums;
using System.Data;

namespace HRM.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// [DEPRECATED] Implementation of IDataScopingService.
/// Use IDataScopeRuleProvider + SqlScopeWhereBuilder instead.
///
/// Lives in Identity.Infrastructure — uses Identity-specific vocabulary.
/// </summary>
[Obsolete("Use IDataScopeRuleProvider + SqlScopeWhereBuilder instead. See Scope Specification Pattern.")]
public sealed class DataScopingService : IDataScopingService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDbConnection _connection;
    private DataScopeContext? _cachedScopeContext;

    public DataScopingService(
        ICurrentUserService currentUserService,
        IDbConnection connection)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<DataScopeContext> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedScopeContext is not null)
        {
            return _cachedScopeContext;
        }

        if (!_currentUserService.IsAuthenticated)
        {
            throw new InvalidOperationException(
                "User is not authenticated. Cannot determine data scope."
            );
        }

#pragma warning disable CS0618
        if (_currentUserService.IsOperator())
#pragma warning restore CS0618
        {
            _cachedScopeContext = new DataScopeContext
            {
                AccountType = AccountType.System,
                UserId = _currentUserService.UserId,
                ScopeLevel = null,
                AllowedCompanyIds = new List<Guid>(),
                AllowedDepartmentIds = new List<Guid>(),
                AllowedPositionIds = new List<Guid>()
            };

            return _cachedScopeContext;
        }

        var employeeId = _currentUserService.EmployeeId;
        if (employeeId is null)
        {
            throw new InvalidOperationException(
                "User type is User but EmployeeId is null. Invalid authentication state."
            );
        }

        var scopeLevel = _currentUserService.ScopeLevel ?? ScopeLevel.Employee;

        var assignments = await LoadActiveAssignmentsAsync(employeeId.Value, cancellationToken);

        var context = new DataScopeContext
        {
            AccountType = AccountType.Employee,
            UserId = _currentUserService.UserId,
            ScopeLevel = scopeLevel,
            AllowedCompanyIds = assignments.Select(a => a.CompanyId).Distinct().ToList(),
            AllowedDepartmentIds = assignments.Select(a => a.DepartmentId).Distinct().ToList(),
            AllowedPositionIds = assignments.Select(a => a.PositionId).Distinct().ToList()
        };

        _cachedScopeContext = context;
        return _cachedScopeContext;
    }

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

#pragma warning disable CS0618
        if (scopeContext.IsOperator)
#pragma warning restore CS0618
        {
            return string.Empty;
        }

        return scopeContext.ScopeLevel switch
        {
            ScopeLevel.Company => BuildCompanyFilter(scopeContext, parameters),
            ScopeLevel.Department => BuildDepartmentFilter(scopeContext, parameters),
            ScopeLevel.Position => BuildPositionFilter(scopeContext, parameters),
            ScopeLevel.Employee => BuildEmployeeFilter(scopeContext, parameters),
            _ => throw new InvalidOperationException($"Unknown scope level: {scopeContext.ScopeLevel}")
        };
    }

    public async Task<bool> CanAccessEmployeeAsync(
        DataScopeContext scopeContext,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (scopeContext is null)
        {
            throw new ArgumentNullException(nameof(scopeContext));
        }

#pragma warning disable CS0618
        if (scopeContext.IsOperator)
#pragma warning restore CS0618
        {
            return true;
        }

        if (scopeContext.ScopeLevel == ScopeLevel.Employee)
        {
            return employeeId == scopeContext.UserId;
        }

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

        var queryParameters = new
        {
            EmployeeId = employeeId,
            AllowedCompanyIds = scopeContext.AllowedCompanyIds,
            AllowedDepartmentIds = scopeContext.AllowedDepartmentIds,
            AllowedPositionIds = scopeContext.AllowedPositionIds
        };

        var count = await _connection.ExecuteScalarAsync<int>(sql, queryParameters);
        return count > 0;
    }

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
            return "AND 1 = 0";
        }

        parameters.AllowedCompanyIds = context.AllowedCompanyIds;
        return "AND ea.CompanyId IN @AllowedCompanyIds";
    }

    private static string BuildDepartmentFilter(DataScopeContext context, dynamic parameters)
    {
        if (!context.AllowedDepartmentIds.Any())
        {
            return "AND 1 = 0";
        }

        parameters.AllowedDepartmentIds = context.AllowedDepartmentIds;
        return "AND ea.DepartmentId IN @AllowedDepartmentIds";
    }

    private static string BuildPositionFilter(DataScopeContext context, dynamic parameters)
    {
        if (!context.AllowedPositionIds.Any())
        {
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

    private sealed class AssignmentDto
    {
        public Guid CompanyId { get; init; }
        public Guid DepartmentId { get; init; }
        public Guid PositionId { get; init; }
    }
}
