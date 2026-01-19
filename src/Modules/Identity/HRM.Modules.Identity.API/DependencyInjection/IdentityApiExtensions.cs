using HRM.Modules.Identity.API.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace HRM.Modules.Identity.API.DependencyInjection;

/// <summary>
/// Extension methods for registering Identity API endpoints
/// Maps Minimal API routes for operator operations
///
/// Usage (Startup/Program.cs):
/// <code>
/// var app = builder.Build();
///
/// // Map Identity endpoints
/// app.MapIdentityEndpoints();
///
/// app.Run();
/// </code>
///
/// Endpoints Registered:
/// - POST /api/identity/operators/register
/// - POST /api/identity/operators/{id}/activate
///
/// Authorization:
/// - All endpoints require authentication (Bearer JWT)
/// - Policy-based authorization: "AdminOnly" policy
/// - Configure policy in Program.cs:
/// <code>
/// builder.Services.AddAuthorization(options =>
/// {
///     options.AddPolicy("AdminOnly", policy =>
///         policy.RequireClaim("role", "Admin"));
/// });
/// </code>
///
/// OpenAPI/Swagger:
/// - Endpoints tagged as "Operators"
/// - WithSummary/WithDescription for documentation
/// - Produces/ProducesProblem for response types
/// </summary>
public static class IdentityApiExtensions
{
    /// <summary>
    /// Map Identity module endpoints
    /// </summary>
    /// <param name="app">Endpoint route builder</param>
    /// <returns>Endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        // Map operator endpoints
        app.MapOperatorEndpoints();

        // Future: Map other endpoint groups
        // app.MapAuthenticationEndpoints();
        // app.MapUserEndpoints();

        return app;
    }
}
