using HRM.Api.DependencyInjection;
using HRM.BuildingBlocks.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. SERVICE REGISTRATION
// ============================================================================

// Add Controllers and Minimal API support
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "HRM API",
        Version = "v1",
        Description = "Human Resource Management System - Modular Monolith Architecture"
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter your token below."
    });

    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
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

// Configure Swagger for Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HRM API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger at root: http://localhost:5000/
    });
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
