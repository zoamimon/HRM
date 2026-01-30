using HRM.BuildingBlocks.Domain.Abstractions.Security;

namespace HRM.BuildingBlocks.Application.Abstractions.Authorization;

/// <summary>
/// Contract for data scope resolution (business module concern).
///
/// This interface lives in BuildingBlocks as a CONTRACT.
/// Implementation lives in the business module (HRM/Personnel)
/// that owns ScopeLevel, org tree, and employee assignments.
///
/// Design (separation of concerns):
/// - IPermissionService (Identity): "Can this user do this?" (action-based)
/// - IDataScopeService (Business): "What data can they see?" (data range)
///
/// Flow:
/// 1. RoutePermissionMiddleware checks permission (IPermissionService)
/// 2. If RequiresDataScope == true, downstream handler calls IDataScopeService
/// 3. IDataScopeService returns DataScopeRule (shared contract)
/// 4. Query handler applies DataScopeRule via EfScopeExpressionBuilder
///
/// Identity module does NOT implement this — the business module does.
/// </summary>
public interface IDataScopeService
{
    /// <summary>
    /// Get the data scope rule for the current user and permission.
    /// Returns a DataScopeRule that can be applied to queries.
    /// </summary>
    /// <param name="userId">Current user ID</param>
    /// <param name="permission">Permission key (e.g., "Personnel.Employee.View")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DataScopeRule containing allowed IDs and scope type</returns>
    Task<DataScopeRule> GetScopeRuleAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default);
}
