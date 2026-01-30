using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Application.Abstractions.Data;

/// <summary>
/// [DEPRECATED] Service for applying data scoping filters based on user's scope level.
/// Use IDataScopeRuleProvider + SqlScopeWhereBuilder instead.
///
/// Lives in Identity module — uses Identity-specific vocabulary (AccountType, ScopeLevel).
/// </summary>
[Obsolete("Use IDataScopeRuleProvider + SqlScopeWhereBuilder instead")]
public interface IDataScopingService
{
    Task<DataScopeContext> GetCurrentScopeAsync(CancellationToken cancellationToken = default);
    string BuildScopeFilter(DataScopeContext scopeContext, dynamic parameters);
    Task<bool> CanAccessEmployeeAsync(
        DataScopeContext scopeContext,
        Guid employeeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// [DEPRECATED] Contains data scope information for the current user.
/// </summary>
[Obsolete("Use DataScopeContext from Identity.Application.Abstractions.Authorization instead")]
public sealed class DataScopeContext
{
    public required AccountType AccountType { get; init; }

    [Obsolete("Use AccountType instead")]
#pragma warning disable CS0618
    public UserType UserType => AccountType.ToUserType();
#pragma warning restore CS0618

    public required Guid UserId { get; init; }
    public ScopeLevel? ScopeLevel { get; init; }
    public List<Guid> AllowedCompanyIds { get; init; } = new();
    public List<Guid> AllowedDepartmentIds { get; init; } = new();
    public List<Guid> AllowedPositionIds { get; init; } = new();

    public bool IsSystemAccount => AccountType == AccountType.System;

    [Obsolete("Use IsSystemAccount instead")]
    public bool IsOperator => IsSystemAccount;

    public bool IsEmployeeAccount => AccountType == AccountType.Employee;

    [Obsolete("Use IsEmployeeAccount instead")]
    public bool IsUser => IsEmployeeAccount;

    public bool RequiresScoping => AccountType == AccountType.Employee;
}
