using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.BuildingBlocks.Infrastructure.Authorization;

/// <summary>
/// Dependency injection extensions for permission-based authorization
///
/// Design:
/// - Registers authorization handler and policy provider
/// - Integrates with ASP.NET Core Authorization middleware
/// - IPermissionService must be registered separately (by Identity module)
///
/// Registration Order:
/// 1. AddPermissionAuthorization() - registers handler and policy provider
/// 2. Identity module registers IPermissionService implementation
/// 3. UseAuthorization() in middleware pipeline
///
/// Usage:
/// <code>
/// // In Program.cs or Startup.cs
/// builder.Services.AddAuthentication(...);
/// builder.Services.AddPermissionAuthorization();
///
/// // Identity module registers IPermissionService
/// builder.Services.AddIdentityInfrastructure(configuration);
///
/// // Middleware
/// app.UseAuthentication();
/// app.UseAuthorization();
/// </code>
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Add permission-based authorization services
    ///
    /// Registers:
    /// - IAuthorizationPolicyProvider: PermissionPolicyProvider (dynamic policy creation)
    /// - IAuthorizationHandler: PermissionAuthorizationHandler (permission checking)
    ///
    /// Note: IPermissionService must be registered separately
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        // Register dynamic policy provider
        // Singleton: Creates policies on-demand, stateless
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        // Register authorization handler
        // Scoped: May need scoped services (like DbContext) for permission checks
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    /// <summary>
    /// Add permission-based authorization with custom configuration
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(
        this IServiceCollection services,
        Action<AuthorizationOptions> configure)
    {
        services.AddAuthorization(configure);
        services.AddPermissionAuthorization();

        return services;
    }
}
