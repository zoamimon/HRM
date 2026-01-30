using System.Security.Claims;
using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using HRM.BuildingBlocks.Domain.Abstractions.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRM.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Middleware that enforces route-based permission security.
///
/// Pipeline Flow:
/// 1. Check if route is public -> Allow
/// 2. Check if user is authenticated -> Deny if not
/// 3. Lookup route permission requirement
/// 4. Check if user has permission (pure action check, NO scope)
/// 5. Store RequiresDataScope flag for downstream handlers
/// 6. Allow or Deny
///
/// Design (separation of concerns):
/// - This middleware ONLY checks permissions (action-based)
/// - Data scope filtering is handled by business module (IDataScopeService)
/// - RequiresDataScope flag is stored in HttpContext.Items for downstream use
/// </summary>
public sealed class RoutePermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRouteSecurityService _routeSecurityService;
    private readonly IOptions<RouteSecurityOptions> _options;
    private readonly ILogger<RoutePermissionMiddleware> _logger;

    public RoutePermissionMiddleware(
        RequestDelegate next,
        IRouteSecurityService routeSecurityService,
        IOptions<RouteSecurityOptions> options,
        ILogger<RoutePermissionMiddleware> logger)
    {
        _next = next;
        _routeSecurityService = routeSecurityService;
        _options = options;
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

        // If route not found in security map, apply DenyByDefault policy
        if (routeEntry == null)
        {
            if (_options.Value.DenyByDefault)
            {
                _logger.LogWarning(
                    "Route not in security map, denying by default (DenyByDefault=true): {Method} {Path}",
                    method, path);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://httpstatuses.io/403",
                    title = "Forbidden",
                    status = 403,
                    detail = "Route not configured in security map. Access denied by default policy."
                });
                return;
            }

            _logger.LogDebug(
                "Route not in security map, allowing (DenyByDefault=false): {Method} {Path}",
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

        // 5. Check permission (pure action check — no scope)
        var hasPermission = await permissionService.HasPermissionAsync(
            userId,
            routeEntry.Permission,
            context.RequestAborted);

        if (!hasPermission)
        {
            _logger.LogWarning(
                "Permission denied for user {UserId}: {Permission} on {Method} {Path}",
                userId, routeEntry.Permission, method, path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.io/403",
                title = "Forbidden",
                status = 403,
                detail = $"Permission required: {routeEntry.Permission}",
                permission = routeEntry.Permission
            });
            return;
        }

        _logger.LogDebug(
            "Permission granted for user {UserId}: {Permission} on {Method} {Path}",
            userId, routeEntry.Permission, method, path);

        // 6. Store context for downstream handlers
        context.Items["CurrentPermission"] = routeEntry.Permission;
        context.Items["RequiresDataScope"] = routeEntry.RequiresDataScope;

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
