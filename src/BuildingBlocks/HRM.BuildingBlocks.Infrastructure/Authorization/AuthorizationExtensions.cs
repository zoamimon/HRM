using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// [DEPRECATED] Dependency injection extensions for permission-based authorization
///
/// IMPORTANT: This extension is deprecated. Use RoutePermissionMiddleware instead.
/// RouteSecurityMap.xml is the Single Source of Truth for endpoint authorization.
///
/// New Architecture:
/// - Remove AddPermissionAuthorization() from Program.cs
/// - Keep only AddAuthorization() for infrastructure endpoints
/// - Add routes to RouteSecurityMap.xml (each module has its own XML)
/// - app.UseRoutePermissions() handles all authorization
///
/// Authorization Layers:
/// - Endpoint protection: RouteSecurityMap.xml (Single Source of Truth)
/// - Data filtering: IPermissionFilterService
/// - Role → Permission mapping: Identity module (database)
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// [DEPRECATED] Add permission-based authorization services
    /// Use RoutePermissionMiddleware and RouteSecurityMap.xml instead.
    /// </summary>
    [Obsolete("Use RoutePermissionMiddleware instead. Remove this call from Program.cs.")]
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        // Register dynamic policy provider
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        // Register authorization handler
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
#pragma warning restore CS0618

        return services;
    }

    /// <summary>
    /// [DEPRECATED] Add permission-based authorization with custom configuration
    /// Use RoutePermissionMiddleware and RouteSecurityMap.xml instead.
    /// </summary>
    [Obsolete("Use RoutePermissionMiddleware instead. Remove this call from Program.cs.")]
    public static IServiceCollection AddPermissionAuthorization(
        this IServiceCollection services,
        Action<AuthorizationOptions> configure)
    {
        services.AddAuthorization(configure);
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddPermissionAuthorization();
#pragma warning restore CS0618

        return services;
    }
}
