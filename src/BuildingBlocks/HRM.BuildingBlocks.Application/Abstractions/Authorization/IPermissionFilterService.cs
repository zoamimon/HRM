using System.Linq.Expressions;
using HRM.BuildingBlocks.Domain.Abstractions.Security;

namespace HRM.BuildingBlocks.Application.Abstractions.Authorization;

/// <summary>
/// Service for applying permission-based query filters.
/// Uses DataScopeRule (from IDataScopeService) to build EF filter expressions.
/// </summary>
public interface IPermissionFilterService
{
    /// <summary>
    /// Get the filter expression for an entity based on current user context
    /// </summary>
    Expression<Func<TEntity, bool>>? GetFilter<TEntity>(
        string permission,
        PermissionFilterContext context) where TEntity : class;

    /// <summary>
    /// Apply the filter to a queryable
    /// </summary>
    IQueryable<TEntity> ApplyFilter<TEntity>(
        IQueryable<TEntity> query,
        string permission,
        PermissionFilterContext context) where TEntity : class;
}
