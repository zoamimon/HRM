using System.Linq.Expressions;
using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using HRM.BuildingBlocks.Domain.Abstractions.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Service for managing and applying permission-based query filters.
/// Retrieves filters from DI and applies them to queries.
///
/// Design: Uses DataScopeRule (resolved by IDataScopeService in business module)
/// to build permission filter expressions. No ScopeLevel dependency.
/// </summary>
public sealed class PermissionFilterService : IPermissionFilterService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionFilterService> _logger;

    public PermissionFilterService(
        IServiceProvider serviceProvider,
        ILogger<PermissionFilterService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Expression<Func<TEntity, bool>>? GetFilter<TEntity>(
        string permission,
        PermissionFilterContext context) where TEntity : class
    {
        // Get all registered filters for this entity type
        var filters = _serviceProvider.GetServices<IPermissionQueryFilter<TEntity>>();

        // Find the filter that matches the permission
        var filter = filters.FirstOrDefault(f =>
            f.Permission.Equals(permission, StringComparison.OrdinalIgnoreCase));

        if (filter == null)
        {
            _logger.LogDebug(
                "No permission filter registered for {Permission} on {Entity}",
                permission,
                typeof(TEntity).Name);
            return null;
        }

        return filter.Build(context);
    }

    /// <inheritdoc />
    public IQueryable<TEntity> ApplyFilter<TEntity>(
        IQueryable<TEntity> query,
        string permission,
        PermissionFilterContext context) where TEntity : class
    {
        var filterExpression = GetFilter<TEntity>(permission, context);

        if (filterExpression == null)
        {
            _logger.LogDebug(
                "No filter applied for {Permission} on {Entity}, returning original query",
                permission,
                typeof(TEntity).Name);
            return query;
        }

        _logger.LogDebug(
            "Applying permission filter for {Permission} on {Entity}",
            permission,
            typeof(TEntity).Name);

        return query.Where(filterExpression);
    }
}
