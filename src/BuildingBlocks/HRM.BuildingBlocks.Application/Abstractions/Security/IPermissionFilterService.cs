using System.Linq.Expressions;
using HRM.BuildingBlocks.Domain.Abstractions.Security;

namespace HRM.BuildingBlocks.Application.Abstractions.Security;

/// <summary>
/// Service for applying permission-based query filters
/// Retrieves the appropriate filter and builds the expression
/// </summary>
public interface IPermissionFilterService
{
    /// <summary>
    /// Get the filter expression for an entity based on current user context
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="permission">Permission key (e.g., "Identity.Operator.View")</param>
    /// <param name="context">User context with scope information</param>
    /// <returns>Filter expression or null if no filter registered</returns>
    Expression<Func<TEntity, bool>>? GetFilter<TEntity>(
        string permission,
        PermissionFilterContext context) where TEntity : class;

    /// <summary>
    /// Apply the filter to a queryable
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="query">Original query</param>
    /// <param name="permission">Permission key</param>
    /// <param name="context">User context</param>
    /// <returns>Filtered query</returns>
    IQueryable<TEntity> ApplyFilter<TEntity>(
        IQueryable<TEntity> query,
        string permission,
        PermissionFilterContext context) where TEntity : class;

    /// <summary>
    /// Build filter context from HTTP context or user claims
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="permission">Permission key</param>
    /// <param name="scope">User's scope for this permission</param>
    /// <param name="departmentId">User's department (optional)</param>
    /// <param name="companyId">User's company (optional)</param>
    /// <returns>Filter context</returns>
    PermissionFilterContext BuildContext(
        Guid userId,
        string permission,
        PermissionScope scope,
        Guid? departmentId = null,
        Guid? companyId = null);
}
