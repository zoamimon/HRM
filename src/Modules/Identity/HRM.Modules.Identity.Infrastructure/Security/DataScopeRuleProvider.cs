using System.Data;
using Dapper;
using HRM.BuildingBlocks.Domain.Abstractions.Security;
using HRM.Modules.Identity.Application.Abstractions.Authorization;
using HRM.Modules.Identity.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HRM.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Implementation of IDataScopeRuleProvider.
/// SINGLE SOURCE OF TRUTH for all data scoping logic.
/// Lives in Identity.Infrastructure — scope business rules belong to Identity module.
/// </summary>
public sealed class DataScopeRuleProvider : IDataScopeRuleProvider
{
    private readonly IDbConnection _connection;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DataScopeRuleProvider> _logger;

    private DataScopeRule? _cachedRule;
    private DataScopeContext? _cachedContext;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public DataScopeRuleProvider(
        IDbConnection connection,
        IMemoryCache cache,
        ILogger<DataScopeRuleProvider> logger)
    {
        _connection = connection;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DataScopeRule> GetRuleAsync(
        DataScopeContext context,
        CancellationToken cancellationToken = default)
    {
        if (_cachedRule != null && ContextMatches(context))
        {
            return _cachedRule;
        }

        var rule = await BuildRuleAsync(context, cancellationToken);
        _cachedRule = rule;
        _cachedContext = context;

        return rule;
    }

    /// <inheritdoc />
    public DataScopeRule GetRule(DataScopeContext context)
    {
        if (_cachedRule != null && ContextMatches(context))
        {
            return _cachedRule;
        }

        var rule = BuildRuleSync(context);
        _cachedRule = rule;
        _cachedContext = context;

        return rule;
    }

    private bool ContextMatches(DataScopeContext context)
    {
        if (_cachedContext == null) return false;

        return _cachedContext.UserId == context.UserId
            && _cachedContext.Permission == context.Permission
            && _cachedContext.ScopeLevel == context.ScopeLevel;
    }

    private async Task<DataScopeRule> BuildRuleAsync(
        DataScopeContext context,
        CancellationToken cancellationToken)
    {
        if (context.IsSystemAccount)
        {
            _logger.LogDebug(
                "System account {UserId} granted Global access for {Permission}",
                context.UserId, context.Permission);

            return DataScopeRule.Global();
        }

        return context.ScopeLevel switch
        {
            ScopeLevel.Global => DataScopeRule.Global(),
            ScopeLevel.Company => await BuildCompanyScopeRuleAsync(context, cancellationToken),
            ScopeLevel.Department => await BuildDepartmentScopeRuleAsync(context, cancellationToken),
            ScopeLevel.Position => await BuildPositionScopeRuleAsync(context, cancellationToken),
            ScopeLevel.Employee => BuildEmployeeScopeRule(context),
            _ => DataScopeRule.None()
        };
    }

    private DataScopeRule BuildRuleSync(DataScopeContext context)
    {
        if (context.IsSystemAccount)
        {
            return DataScopeRule.Global();
        }

        return context.ScopeLevel switch
        {
            ScopeLevel.Global => DataScopeRule.Global(),
            ScopeLevel.Employee => BuildEmployeeScopeRule(context),
            _ => DataScopeRule.None()
        };
    }

    private async Task<DataScopeRule> BuildCompanyScopeRuleAsync(
        DataScopeContext context,
        CancellationToken cancellationToken)
    {
        if (!context.EmployeeId.HasValue)
        {
            _logger.LogWarning(
                "Company scope requested but no EmployeeId for user {UserId}",
                context.UserId);
            return DataScopeRule.None();
        }

        var assignments = await LoadEmployeeAssignmentsAsync(
            context.EmployeeId.Value, cancellationToken);

        if (assignments.Count == 0)
        {
            _logger.LogWarning(
                "No active assignments found for employee {EmployeeId}",
                context.EmployeeId.Value);
            return DataScopeRule.None();
        }

        var companyIds = assignments
            .Select(a => a.CompanyId)
            .Distinct()
            .ToList();

        _logger.LogDebug(
            "Company scope for user {UserId}: {CompanyCount} companies",
            context.UserId, companyIds.Count);

        return DataScopeRule.ForCompanies(companyIds, context.UserId);
    }

    private async Task<DataScopeRule> BuildDepartmentScopeRuleAsync(
        DataScopeContext context,
        CancellationToken cancellationToken)
    {
        if (!context.EmployeeId.HasValue)
        {
            return DataScopeRule.None();
        }

        var assignments = await LoadEmployeeAssignmentsAsync(
            context.EmployeeId.Value, cancellationToken);

        if (assignments.Count == 0)
        {
            return DataScopeRule.None();
        }

        var departmentIds = assignments
            .Select(a => a.DepartmentId)
            .Distinct()
            .ToList();

        _logger.LogDebug(
            "Department scope for user {UserId}: {DepartmentCount} departments",
            context.UserId, departmentIds.Count);

        return DataScopeRule.ForDepartments(departmentIds, context.UserId);
    }

    private async Task<DataScopeRule> BuildPositionScopeRuleAsync(
        DataScopeContext context,
        CancellationToken cancellationToken)
    {
        if (!context.EmployeeId.HasValue)
        {
            return DataScopeRule.None();
        }

        var assignments = await LoadEmployeeAssignmentsAsync(
            context.EmployeeId.Value, cancellationToken);

        if (assignments.Count == 0)
        {
            return DataScopeRule.None();
        }

        var positionIds = assignments
            .Select(a => a.PositionId)
            .Distinct()
            .ToList();

        _logger.LogDebug(
            "Position scope for user {UserId}: {PositionCount} positions",
            context.UserId, positionIds.Count);

        return DataScopeRule.ForPositions(positionIds, context.UserId);
    }

    private DataScopeRule BuildEmployeeScopeRule(DataScopeContext context)
    {
        _logger.LogDebug(
            "Employee scope for user {UserId}, employeeId {EmployeeId}",
            context.UserId, context.EmployeeId);

        return DataScopeRule.ForSelf(context.UserId, context.EmployeeId);
    }

    private async Task<List<EmployeeAssignmentDto>> LoadEmployeeAssignmentsAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"EmployeeAssignments_{employeeId}";

        if (_cache.TryGetValue<List<EmployeeAssignmentDto>>(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        const string sql = """
            SELECT
                CompanyId,
                DepartmentId,
                PositionId
            FROM personnel.EmployeeAssignments
            WHERE EmployeeId = @EmployeeId
                AND (EndDate IS NULL OR EndDate > GETUTCDATE())
            """;

        var assignments = (await _connection.QueryAsync<EmployeeAssignmentDto>(
            sql,
            new { EmployeeId = employeeId }
        )).ToList();

        _cache.Set(cacheKey, assignments, CacheDuration);

        return assignments;
    }

    private sealed class EmployeeAssignmentDto
    {
        public Guid CompanyId { get; init; }
        public Guid DepartmentId { get; init; }
        public Guid PositionId { get; init; }
    }
}
