using System.Text.Json.Serialization;
using HRM.Api.DependencyInjection;
using HRM.BuildingBlocks.Infrastructure.DependencyInjection;
using HRM.BuildingBlocks.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. SERVICE REGISTRATION
// ============================================================================

// Add Controllers and Minimal API support
builder.Services.AddEndpointsApiExplorer();

// Configure JSON serialization options for Minimal APIs
// - Serialize enums as strings (e.g., "Active" instead of 1)
// - Use camelCase for property names
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure OpenAPI (.NET 10 Native - Minimal Approach)
// Security is auto-detected from .RequireAuthorization() on endpoints
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        // Set document metadata only
        document.Info = new()
        {
            Title = "HRM API",
            Version = "v1",
            Description = "Human Resource Management System - Modular Monolith Architecture"
        };

        return Task.CompletedTask;
    });
});

// Configure CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add JWT Authentication
// Must be called BEFORE AddModules() to ensure authentication is configured
// before modules that depend on it
builder.Services.AddJwtAuthentication(builder.Configuration);

// Add Authorization (minimal - infrastructure only)
// NOTE: Business endpoint authorization is handled by RoutePermissionMiddleware
// which reads permissions from RouteSecurityMap.xml (single source of truth)
//
// ASP.NET Authorization is only used for:
// - Infrastructure endpoints (health, openapi) via .AllowAnonymous()
// - Basic [Authorize] attribute for OpenAPI documentation
//
// DO NOT add business policies here - use RouteSecurityMap.xml instead
builder.Services.AddAuthorization();

// Register all HRM modules (BuildingBlocks + Identity + future modules)
// This registers:
// - BuildingBlocks Infrastructure (MediatR, EventBus, CurrentUserService, RolesClaimsTransformation, etc.)
// - Identity Module Infrastructure (DbContext, repositories, services)
builder.Services.AddModules(builder.Configuration);

// ============================================================================
// 2. MIDDLEWARE PIPELINE CONFIGURATION
// ============================================================================

var app = builder.Build();

// Map OpenAPI endpoint (.NET 10 Standard)
// OpenAPI JSON available at: /openapi/v1.json
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowFrontend");

// Authentication & Authorization middleware
// IMPORTANT: Order matters!
// 1. UseAuthentication() - validates JWT and populates HttpContext.User
// 2. UseAuthorization() - checks [Authorize] attributes and policies
// 3. UseRoutePermissions() - checks route-level permissions from RouteSecurityMap.xml
app.UseAuthentication();
app.UseAuthorization();
app.UseRoutePermissions();

// Map all module endpoints
// - Identity: POST /api/identity/operators/register, POST /api/identity/operators/{id}/activate
// - Future modules will be added here
app.MapModuleEndpoints();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Environment = app.Environment.EnvironmentName
}))
.WithName("HealthCheck")
.WithTags("System")
.AllowAnonymous();

// ============================================================================
// 3. RUN APPLICATION
// ============================================================================

app.Run();
