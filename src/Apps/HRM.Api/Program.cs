using HRM.Api.DependencyInjection;
using HRM.BuildingBlocks.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. SERVICE REGISTRATION
// ============================================================================

// Add Controllers and Minimal API support
builder.Services.AddEndpointsApiExplorer();

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

// Add Authorization with policies
builder.Services.AddAuthorization(options =>
{
    // Admin-only policy for operator management
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    // Manager policy for department/employee management
    options.AddPolicy("Manager", policy =>
        policy.RequireRole("Admin", "Manager"));

    // Authenticated user policy
    options.AddPolicy("User", policy =>
        policy.RequireAuthenticatedUser());
});

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
app.UseAuthentication();
app.UseAuthorization();

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
