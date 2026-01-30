using HRM.BuildingBlocks.Domain.Abstractions.Security;
using HRM.Modules.Identity.Domain.Enums;

namespace HRM.Modules.Identity.Application.Abstractions.Authorization;

/// <summary>
/// Context for building data scope rules.
/// Contains user information needed to determine scope.
/// Identity module internal — other modules receive DataScopeRule (contract), not this.
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
    /// True if user is a system account (Operator).
    /// System accounts typically have global access.
    /// </summary>
    public bool IsSystemAccount { get; init; }

    /// <summary>
    /// User's employee ID (for employee accounts).
    /// Null for system accounts.
    /// </summary>
    public Guid? EmployeeId { get; init; }
}

/// <summary>
/// Single Source of Truth for data scope rules.
/// Lives in Identity module — this is where ALL scope logic lives.
///
/// Other modules receive DataScopeRule (a shared contract in BuildingBlocks.Domain)
/// and translate it to EF expressions or SQL WHERE clauses.
/// They never reference ScopeLevel or DataScopeContext directly.
/// </summary>
public interface IDataScopeRuleProvider
{
    /// <summary>
    /// Get the data scope rule for the current context.
    /// This is the ONLY place where scope logic is implemented.
    /// </summary>
    Task<DataScopeRule> GetRuleAsync(DataScopeContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get rule synchronously (for cached/in-memory scenarios).
    /// Use GetRuleAsync when database lookup is needed.
    /// </summary>
    DataScopeRule GetRule(DataScopeContext context);
}
