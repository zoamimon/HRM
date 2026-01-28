using HRM.BuildingBlocks.Domain.Abstractions.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Hosted service that loads all registered RouteSecurityMap resources at application startup
///
/// Design:
/// - Modules register their RouteSecurityMap sources via IOptions&lt;RouteSecurityOptions&gt;
/// - This service loads all sources when the application starts
/// - Ensures the singleton RouteSecurityService instance has all route configurations
///
/// Why IHostedService?
/// - Runs after DI container is fully built (avoids BuildServiceProvider anti-pattern)
/// - Singleton services are already resolved correctly
/// - Can log startup errors clearly
/// - Executes before request handling begins
///
/// Usage:
/// 1. Module registers source during DI:
///    services.Configure&lt;RouteSecurityOptions&gt;(options =&gt;
///        options.Sources.Add(new RouteSecurityMapSourceConfig { ... }));
///
/// 2. This service loads all sources at startup:
///    foreach (var source in options.Sources) service.LoadFromEmbeddedResource(...)
/// </summary>
public sealed class RouteSecurityLoaderService : IHostedService
{
    private readonly IRouteSecurityService _routeSecurityService;
    private readonly RouteSecurityOptions _options;
    private readonly ILogger<RouteSecurityLoaderService> _logger;

    public RouteSecurityLoaderService(
        IRouteSecurityService routeSecurityService,
        IOptions<RouteSecurityOptions> options,
        ILogger<RouteSecurityLoaderService> logger)
    {
        _routeSecurityService = routeSecurityService;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading RouteSecurityMap configurations...");

        var sources = _options.Sources;

        if (sources.Count == 0)
        {
            _logger.LogWarning("No RouteSecurityMap sources registered. Route-based security will not be active.");
            return Task.CompletedTask;
        }

        foreach (var source in sources)
        {
            try
            {
                _logger.LogDebug(
                    "Loading RouteSecurityMap from {Assembly}: {ResourceName}",
                    source.Assembly.GetName().Name,
                    source.ResourceName);

                _routeSecurityService.LoadFromEmbeddedResource(source.Assembly, source.ResourceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load RouteSecurityMap from {Assembly}: {ResourceName}",
                    source.Assembly.GetName().Name,
                    source.ResourceName);
            }
        }

        // Validate configuration
        var errors = _routeSecurityService.ValidateConfiguration();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                _logger.LogWarning("RouteSecurityMap validation warning: {Error}", error);
            }
        }

        var publicRoutes = _routeSecurityService.GetPublicRoutes();
        var protectedRoutes = _routeSecurityService.GetProtectedRoutes();

        _logger.LogInformation(
            "RouteSecurityMap loaded: {PublicCount} public routes, {ProtectedCount} protected routes from {SourceCount} sources",
            publicRoutes.Count,
            protectedRoutes.Count,
            sources.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
