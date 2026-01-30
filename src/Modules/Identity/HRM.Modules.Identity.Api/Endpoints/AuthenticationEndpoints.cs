using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Application.Abstractions.Infrastructure;
using HRM.Modules.Identity.Application.Commands.Login;
using HRM.Modules.Identity.Application.Commands.Logout;
using HRM.Modules.Identity.Application.Commands.RefreshToken;
using HRM.Modules.Identity.Application.Commands.RevokeAllSessionsExceptCurrent;
using HRM.Modules.Identity.Application.Commands.RevokeSession;
using HRM.Modules.Identity.Application.Queries.GetActiveSessions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HRM.Modules.Identity.Api.Endpoints;

/// <summary>
/// Authentication endpoints for Identity module
/// Handles login, logout, token refresh, and session management
///
/// Endpoints:
/// - POST /api/identity/auth/login - Authenticate and get tokens
/// - POST /api/identity/auth/logout - Logout (revoke refresh token)
/// - POST /api/identity/auth/refresh - Refresh access token
/// - GET /api/identity/auth/sessions - Get active sessions
/// - DELETE /api/identity/auth/sessions/{id} - Revoke specific session
/// - POST /api/identity/auth/sessions/revoke-all-except-current - Logout all other devices
///
/// Token Storage Strategy:
/// - Web: Access token in memory (JS), Refresh token in HttpOnly cookie
/// - Mobile: Both tokens in request/response body (stored in secure storage)
///
/// Security Architecture:
/// - Login/Refresh: Public routes (defined in RouteSecurityMap.xml)
/// - Logout/Sessions: Protected routes (RoutePermissionMiddleware checks permissions)
/// - .RequireAuthorization() for OpenAPI docs (lock icon) only
/// - Actual auth: RouteSecurityMap.xml is Single Source of Truth
/// - HTTPS enforced in production
/// - CORS configured for allowed origins
/// </summary>
public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity/auth")
            .WithTags("Authentication")
            .WithOpenApi();

        // ================================================================
        // POST /api/identity/auth/login
        // Authenticate operator and return access + refresh tokens
        // ================================================================
        group.MapPost("/login", async (
            HttpContext httpContext,
            LoginRequest request,
            ISender sender,
            IClientInfoService clientInfo) =>
        {
            var command = new LoginCommand(
                request.UsernameOrEmail,
                request.Password,
                request.RememberMe,
                clientInfo.IpAddress,
                clientInfo.UserAgent
            );

            var result = await sender.Send(command);

            return result.Match(
                onSuccess: response =>
                {
                    // Set HttpOnly cookie for web clients (XSS protection)
                    httpContext.Response.Cookies.Append("refreshToken",
                        response.RefreshToken,
                        new CookieOptions
                        {
                            HttpOnly = true,      // Cannot be accessed by JavaScript
                            Secure = true,        // HTTPS only
                            SameSite = SameSiteMode.Strict,  // CSRF protection
                            Expires = response.RefreshTokenExpiry,
                            Path = "/"
                        });

                    // Return access token + user info (NOT refresh token in body for web)
                    // Mobile apps can read refresh token from body if cookie not available
                    return Results.Ok(new LoginSuccessResponse
                    {
                        AccessToken = response.AccessToken,
                        AccessTokenExpiry = response.AccessTokenExpiry,
                        RefreshToken = response.RefreshToken, // For mobile apps
                        RefreshTokenExpiry = response.RefreshTokenExpiry,
                        User = response.User
                    });
                },
                onFailure: error => Results.Problem(
                    statusCode: error switch
                    {
                        HRM.BuildingBlocks.Domain.Abstractions.Results.UnauthorizedError => StatusCodes.Status401Unauthorized,
                        HRM.BuildingBlocks.Domain.Abstractions.Results.ForbiddenError => StatusCodes.Status403Forbidden,
                        _ => StatusCodes.Status400BadRequest
                    },
                    title: error.Code,
                    detail: error.Message
                )
            );
        })
        .AllowAnonymous()
        .WithName("Login")
        .WithSummary("Authenticate and get tokens")
        .WithDescription("Authenticates operator with username/email and password. Returns JWT access token and refresh token.")
        .Produces<LoginSuccessResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // ================================================================
        // POST /api/identity/auth/refresh
        // Refresh access token using refresh token
        // ================================================================
        group.MapPost("/refresh", async (
            HttpContext httpContext,
            RefreshRequest? request,
            ISender sender,
            IClientInfoService clientInfo) =>
        {
            // Try to get refresh token from cookie first (web), then body (mobile)
            var refreshToken = httpContext.Request.Cookies["refreshToken"]
                ?? request?.RefreshToken;

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication.InvalidRefreshToken",
                    detail: "Refresh token is missing"
                );
            }

            var command = new RefreshTokenCommand(
                refreshToken,
                clientInfo.IpAddress,
                clientInfo.UserAgent
            );

            var result = await sender.Send(command);

            return result.Match(
                onSuccess: response =>
                {
                    // Update cookie with new refresh token (token rotation)
                    httpContext.Response.Cookies.Append("refreshToken",
                        response.RefreshToken,
                        new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.Strict,
                            Expires = response.RefreshTokenExpiry,
                            Path = "/"
                        });

                    return Results.Ok(new LoginSuccessResponse
                    {
                        AccessToken = response.AccessToken,
                        AccessTokenExpiry = response.AccessTokenExpiry,
                        RefreshToken = response.RefreshToken,
                        RefreshTokenExpiry = response.RefreshTokenExpiry,
                        User = response.User
                    });
                },
                onFailure: error => Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: error.Code,
                    detail: error.Message
                )
            );
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .WithSummary("Refresh access token")
        .WithDescription("Exchanges refresh token for new access token and refresh token (token rotation).")
        .Produces<LoginSuccessResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // ================================================================
        // POST /api/identity/auth/logout
        // Logout and revoke refresh token
        // ================================================================
        group.MapPost("/logout", async (
            HttpContext httpContext,
            LogoutRequest? request,
            ISender sender) =>
        {
            // Try to get refresh token from cookie first, then body
            var refreshToken = httpContext.Request.Cookies["refreshToken"]
                ?? request?.RefreshToken;

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                // AuditBehavior will automatically inject IP/UserAgent
                var command = new LogoutCommand(refreshToken);

                await sender.Send(command);
            }

            // Delete cookie regardless
            httpContext.Response.Cookies.Delete("refreshToken");

            return Results.Ok(new { message = "Logged out successfully" });
        })
        .RequireAuthorization()
        .WithName("Logout")
        .WithSummary("Logout and revoke session")
        .WithDescription("Revokes refresh token to terminate current session. Access token expires naturally.")
        .Produces(StatusCodes.Status200OK);

        // ================================================================
        // GET /api/identity/auth/sessions
        // Get all active sessions (devices)
        // ================================================================
        group.MapGet("/sessions", async (
            HttpContext httpContext,
            ISender sender,
            ICurrentUserService currentUserService) =>
        {
            var currentToken = httpContext.Request.Cookies["refreshToken"];

            var query = new GetActiveSessionsQuery(
                currentUserService.UserId,
                currentToken
            );

            var result = await sender.Send(query);

            return result.Match(
                onSuccess: sessions => Results.Ok(sessions),
                onFailure: error => Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: error.Code,
                    detail: error.Message
                )
            );
        })
        .RequireAuthorization()
        .WithName("GetActiveSessions")
        .WithSummary("Get active sessions")
        .WithDescription("Returns list of all active sessions (logged-in devices) for current user.")
        .Produces<List<SessionInfo>>(StatusCodes.Status200OK);

        // ================================================================
        // DELETE /api/identity/auth/sessions/{sessionId}
        // Revoke specific session (logout from specific device)
        // ================================================================
        group.MapDelete("/sessions/{sessionId:guid}", async (
            Guid sessionId,
            ISender sender,
            ICurrentUserService currentUserService) =>
        {
            // AuditBehavior will automatically inject IP/UserAgent
            var command = new RevokeSessionCommand(
                sessionId,
                currentUserService.UserId
            );

            var result = await sender.Send(command);

            return await result.Match<IResult>(
                onSuccess: () => Task.FromResult(Results.NoContent()),
                onFailure: error => Task.FromResult(
                    Results.Problem(
                        statusCode: error switch
                        {
                            HRM.BuildingBlocks.Domain.Abstractions.Results.NotFoundError => StatusCodes.Status404NotFound,
                            HRM.BuildingBlocks.Domain.Abstractions.Results.ForbiddenError => StatusCodes.Status403Forbidden,
                            _ => StatusCodes.Status400BadRequest
                        },
                        title: error.Code,
                        detail: error.Message
                    )
                )
            );
        })
        .RequireAuthorization()
        .WithName("RevokeSession")
        .WithSummary("Logout from specific device")
        .WithDescription("Revokes specific session by ID. Used for remote device logout.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // ================================================================
        // POST /api/identity/auth/sessions/revoke-all-except-current
        // Logout from all other devices (security feature)
        // ================================================================
        group.MapPost("/sessions/revoke-all-except-current", async (
            HttpContext httpContext,
            ISender sender,
            ICurrentUserService currentUserService) =>
        {
            var currentToken = httpContext.Request.Cookies["refreshToken"];

            if (string.IsNullOrWhiteSpace(currentToken))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Session.CannotIdentifyCurrentSession",
                    detail: "Cannot identify current session. Please log in again."
                );
            }

            // AuditBehavior will automatically inject IP/UserAgent
            var command = new RevokeAllSessionsExceptCurrentCommand(
                currentUserService.UserId,
                currentToken
            );

            var result = await sender.Send(command);

            return result.Match(
                onSuccess: response => Results.Ok(response),
                onFailure: error => Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: error.Code,
                    detail: error.Message
                )
            );
        })
        .RequireAuthorization()
        .WithName("RevokeAllSessionsExceptCurrent")
        .WithSummary("Logout all other devices")
        .WithDescription("Revokes all sessions except current device. Useful for security: suspect account compromise.")
        .Produces<RevokeAllSessionsResult>(StatusCodes.Status200OK);

        return app;
    }
}

// ============================================================================
// Request/Response DTOs
// ============================================================================

/// <summary>
/// Login request DTO
/// </summary>
public sealed record LoginRequest
{
    public required string UsernameOrEmail { get; init; }
    public required string Password { get; init; }
    public bool RememberMe { get; init; } = false;
}

/// <summary>
/// Login success response DTO
/// </summary>
public sealed record LoginSuccessResponse
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiry { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime RefreshTokenExpiry { get; init; }
    public required UserInfo User { get; init; }
}

/// <summary>
/// Refresh token request DTO (for mobile apps)
/// </summary>
public sealed record RefreshRequest
{
    public string? RefreshToken { get; init; }
}

/// <summary>
/// Logout request DTO (for mobile apps)
/// </summary>
public sealed record LogoutRequest
{
    public string? RefreshToken { get; init; }
}
