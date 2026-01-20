using HRM.BuildingBlocks.Infrastructure.DependencyInjection;
using HRM.Modules.Identity.Api.DependencyInjection;
using HRM.Modules.Identity.Infrastructure.DependencyInjection;

namespace HRM.Api.DependencyInjection;

/// <summary>
/// Extension methods for registering all HRM modules
/// Implements Modular Monolith pattern with module composition
///
/// Architecture:
/// - BuildingBlocks: Shared infrastructure (MediatR, Authentication, EventBus, etc.)
/// - Identity Module: Authentication and authorization (Operators, Users)
/// - Personnel Module: Employee management (future)
/// - Attendance Module: Time tracking (future)
///
/// Module Registration Order:
/// 1. BuildingBlocks Infrastructure (shared services)
/// 2. Module-specific Infrastructure (DbContext, repositories, services)
/// 3. Module-specific API (endpoints, contracts)
///
/// Usage (Program.cs):
/// <code>
/// // Register all modules
/// builder.Services.AddModules(builder.Configuration);
///
/// // Map all module endpoints
/// app.MapModuleEndpoints();
/// </code>
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Register all HRM modules with dependency injection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Register BuildingBlocks Infrastructure (shared services)
        // - Event Bus (InMemoryEventBus)
        // - CurrentUserService
        // - RolesClaimsTransformation
        // - MediatR with pipeline behaviors (Logging, Validation, UnitOfWork)
        // - EF Core interceptors (AuditInterceptor)
        services.AddBuildingBlocksInfrastructure(configuration);

        // 2. Register Identity Module Infrastructure
        // - IdentityDbContext with SQL Server
        // - Repositories (IOperatorRepository, IUserRepository)
        // - Authentication services (IPasswordHasher, ITokenService)
        // - MediatR handlers for Identity commands/queries
        services.AddIdentityInfrastructure(configuration);

        // Future modules:
        // services.AddPersonnelInfrastructure(configuration);
        // services.AddAttendanceInfrastructure(configuration);

        return services;
    }

    /// <summary>
    /// Map all module endpoints to the application
    /// </summary>
    /// <param name="app">Endpoint route builder (WebApplication)</param>
    /// <returns>Endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder app)
    {
        // Map Identity module endpoints
        // - POST /api/identity/operators/register
        // - POST /api/identity/operators/{id}/activate
        app.MapIdentityEndpoints();

        // Future module endpoints:
        // app.MapPersonnelEndpoints();
        // app.MapAttendanceEndpoints();

        return app;
    }
}
