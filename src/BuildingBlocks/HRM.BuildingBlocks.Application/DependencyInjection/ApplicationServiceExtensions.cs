using HRM.BuildingBlocks.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.BuildingBlocks.Application.DependencyInjection;

/// <summary>
/// Dependency injection registration for BuildingBlocks Application layer
/// 
/// Responsibilities:
/// - Register MediatR with pipeline behaviors
/// - Configure cross-cutting concerns (logging, validation, transactions)
/// - Provide centralized behavior configuration
/// 
/// Architecture:
/// - Application layer registers MediatR (NOT Infrastructure)
/// - Behaviors are application-level cross-cutting concerns
/// - Infrastructure only provides technical implementations
/// 
/// Usage (API Program.cs):
/// <code>
/// builder.Services
///     .AddBuildingBlocksApplication()      // 1. MediatR + behaviors
///     .AddBuildingBlocksInfrastructure()   // 2. Technical services
///     .AddIdentityApplication()            // 3. Module handlers
///     .AddIdentityInfrastructure();        // 4. Module DbContext + UoW
/// </code>
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Add BuildingBlocks Application services
    /// Registers MediatR with all pipeline behaviors
    /// 
    /// Behavior Order (CRITICAL):
    /// 1. LoggingBehavior    - Log all requests (even failures)
    /// 2. ValidationBehavior - Fail fast before transaction
    /// 3. UnitOfWorkBehavior - Wrap handler with transaction
    /// 
    /// Why This Order:
    /// - Logging first → captures all requests including validation failures
    /// - Validation before UoW → no wasted database connections
    /// - UoW wraps handler → only commit if handler succeeds
    /// </summary>
    public static IServiceCollection AddBuildingBlocksApplication(
        this IServiceCollection services)
    {
        // Register MediatR
        services.AddMediatR(config =>
        {
            // Scan BuildingBlocks.Application assembly for behaviors
            config.RegisterServicesFromAssembly(typeof(ApplicationServiceExtensions).Assembly);

            // Register pipeline behaviors in order
            // Order matters! See documentation above
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            config.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
        });

        return services;
    }
}
