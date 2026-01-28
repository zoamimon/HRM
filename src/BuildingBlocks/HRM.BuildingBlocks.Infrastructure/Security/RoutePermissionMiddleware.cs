using System.Security.Claims;
using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using HRM.BuildingBlocks.Domain.Abstractions.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Middleware that enforces route-based permission security
/// Replaces [HasPermission] attributes with centralized route protection
///
/// Pipeline Flow:
/// 1. Check if route is public -> Allow
/// 2. Check if user is authenticated -> Deny if not
/// 3. Lookup route permission requirement
/// 4. Check if user has permission with required scope
/// 5. Allow or Deny
/// </summary>
public sealed class RoutePermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRouteSecurityService _routeSecurityService;
    private readonly ILogger<RoutePermissionMiddleware> _logger;

    public RoutePermissionMiddleware(
        RequestDelegate next,
        IRouteSecurityService routeSecurityService,
        ILogger<RoutePermissionMiddleware> logger)
    {
        _next = next;
        _routeSecurityService = routeSecurityService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IPermissionService permissionService)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        // 1. Check if route is public
        if (_routeSecurityService.IsPublicRoute(method, path))
        {
            _logger.LogDebug("Public route accessed: {Method} {Path}", method, path);
            await _next(context);
            return;
        }

        // 2. Check if user is authenticated
        if (context.User.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning(
                "Unauthenticated access attempt to protected route: {Method} {Path}",
                method, path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.io/401",
                title = "Unauthorized",
                status = 401,
                detail = "Authentication required"
            });
            return;
        }

        // 3. Lookup route permission requirement
        var routeEntry = _routeSecurityService.GetRouteSecurityEntry(method, path);

        // If route not found in security map, check if there's a fallback behavior
        // For now, we'll allow routes not in the map (backward compatibility)
        // TODO: Make this configurable (DenyByDefault vs AllowByDefault)
        if (routeEntry == null)
        {
            _logger.LogDebug(
                "Route not in security map, allowing by default: {Method} {Path}",
                method, path);
            await _next(context);
            return;
        }

        // 4. Get user ID from claims
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? context.User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning(
                "No user ID in claims for protected route: {Method} {Path}",
                method, path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.io/401",
                title = "Unauthorized",
                status = 401,
                detail = "Invalid authentication token"
            });
            return;
        }

        // 5. Check if user has permission with required scope
        var hasPermission = await permissionService.HasPermissionWithScopeAsync(
            userId,
            routeEntry.Permission,
            routeEntry.MinScope,
            context.RequestAborted);

        if (!hasPermission)
        {
            _logger.LogWarning(
                "Permission denied for user {UserId}: {Permission} with MinScope {MinScope} on {Method} {Path}",
                userId, routeEntry.Permission, routeEntry.MinScope, method, path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.io/403",
                title = "Forbidden",
                status = 403,
                detail = $"Permission required: {routeEntry.Permission}",
                permission = routeEntry.Permission,
                minScope = routeEntry.MinScope.ToString()
            });
            return;
        }

        _logger.LogDebug(
            "Permission granted for user {UserId}: {Permission} on {Method} {Path}",
            userId, routeEntry.Permission, method, path);

        // 6. Store the user's scope in HttpContext for use by query filters
        var userScope = await permissionService.GetPermissionScopeAsync(
            userId,
            routeEntry.Permission,
            context.RequestAborted);

        if (userScope.HasValue)
        {
            context.Items["CurrentPermission"] = routeEntry.Permission;
            context.Items["CurrentPermissionScope"] = userScope.Value;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for adding RoutePermissionMiddleware
/// </summary>
public static class RoutePermissionMiddlewareExtensions
{
    /// <summary>
    /// Add route-based permission middleware to the pipeline
    /// Should be added AFTER UseAuthentication() and UseAuthorization()
    /// </summary>
    public static IApplicationBuilder UseRoutePermissions(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RoutePermissionMiddleware>();
    }
}
