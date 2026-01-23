using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Modules.Identity.Application.DependencyInjection;

/// <summary>
/// Dependency injection registration for Identity module Application layer
/// 
/// Responsibilities:
/// - Register MediatR command/query handlers
/// - Register FluentValidation validators
/// - Module-specific application services
/// 
/// Architecture Principle:
/// - Application layer owns handlers and validators
/// - Infrastructure layer DOES NOT register these
/// - Separation of concerns: Application = use cases, Infrastructure = technical
/// 
/// Clean Architecture Flow:
/// API → Application → Domain
///  ↓
/// Infrastructure (implements, doesn't orchestrate)
/// 
/// Usage (API Program.cs):
/// <code>
/// builder.Services
///     .AddBuildingBlocksApplication()      // 1. MediatR + behaviors (global)
///     .AddBuildingBlocksInfrastructure()   // 2. Technical services
///     .AddIdentityApplication()            // 3. Identity handlers + validators
///     .AddIdentityInfrastructure();        // 4. Identity DbContext + UoW
/// </code>
/// </summary>
public static class IdentityApplicationExtensions
{
    /// <summary>
    /// Add Identity module Application services
    /// Registers handlers and validators from Identity.Application assembly
    /// 
    /// What Gets Registered:
    /// - Command handlers (RegisterOperatorCommandHandler, etc.)
    /// - Query handlers (GetOperatorByIdQueryHandler, etc.)
    /// - Domain event handlers (UserCreatedDomainEventHandler, etc.)
    /// - FluentValidation validators (RegisterOperatorCommandValidator, etc.)
    /// 
    /// Note:
    /// - MediatR itself is registered by BuildingBlocksApplication
    /// - This only registers Identity-specific handlers
    /// - Behaviors are NOT registered here (global in BuildingBlocks)
    /// </summary>
    public static IServiceCollection AddIdentityApplication(
        this IServiceCollection services)
    {
        // Register MediatR handlers from Identity.Application assembly
        // Discovers all ICommandHandler, IQueryHandler, INotificationHandler
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(
                typeof(IdentityApplicationExtensions).Assembly
            );
        });

        // Register FluentValidation validators from Identity.Application assembly
        // Discovers all AbstractValidator<T>
        services.AddValidatorsFromAssembly(
            typeof(IdentityApplicationExtensions).Assembly
        );

        return services;
    }
}
