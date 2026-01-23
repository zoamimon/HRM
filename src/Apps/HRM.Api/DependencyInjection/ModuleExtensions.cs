using HRM.BuildingBlocks.Application.DependencyInjection;
using HRM.BuildingBlocks.Infrastructure.DependencyInjection;
using HRM.Modules.Identity.Api.DependencyInjection;
using HRM.Modules.Identity.Application.DependencyInjection;
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
/// Module Registration Order (CRITICAL - DO NOT CHANGE):
/// 1. BuildingBlocks Application (MediatR + pipeline behaviors)
/// 2. BuildingBlocks Infrastructure (technical services)
/// 3. Module-specific Application (handlers + validators)
/// 4. Module-specific Infrastructure (DbContext + UnitOfWork)
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
        // ====================================================================
        // REGISTRATION ORDER IS CRITICAL - DO NOT REORDER
        // ====================================================================

        // 1. BuildingBlocks Application Layer
        // Register MediatR with pipeline behaviors (BEFORE any handlers)
        // - LoggingBehavior (outermost - logs all requests)
        // - ValidationBehavior (fails fast before transaction)
        // - UnitOfWorkBehavior (wraps handler with transaction)
        services.AddBuildingBlocksApplication();

        // 2. BuildingBlocks Infrastructure Layer
        // Register technical services (AFTER MediatR, BEFORE modules)
        // - Event Bus (InMemoryEventBus)
        // - CurrentUserService (for ICurrentUserService)
        // - RolesClaimsTransformation (JWT role normalization)
        // - AuditInterceptor (Scoped - depends on ICurrentUserService)
        services.AddBuildingBlocksInfrastructure(configuration);

        // 3. Identity Module Application Layer
        // Register module-specific handlers and validators
        // - Command handlers (RegisterOperatorCommandHandler, etc.)
        // - Query handlers (GetOperatorByIdQueryHandler, etc.)
        // - Domain event handlers (OperatorRegisteredDomainEventHandler, etc.)
        // - FluentValidation validators (RegisterOperatorCommandValidator, etc.)
        services.AddIdentityApplication();

        // 4. Identity Module Infrastructure Layer
        // Register module-specific technical implementations
        // - IdentityDbContext (SQL Server, schema: Identity)
        // - IModuleUnitOfWork → IdentityDbContext (for UnitOfWorkBehavior)
        // - Repositories (IOperatorRepository → OperatorRepository)
        // - Authentication services (IPasswordHasher, ITokenService)
        // - Background services (IdentityOutboxProcessor)
        services.AddIdentityInfrastructure(configuration);

        // Future modules (same pattern):
        // services.AddPersonnelApplication();
        // services.AddPersonnelInfrastructure(configuration);
        //
        // services.AddPayrollApplication();
        // services.AddPayrollInfrastructure(configuration);

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
