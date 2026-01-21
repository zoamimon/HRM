using FluentValidation;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Domain.Repositories;
using HRM.Modules.Identity.Infrastructure.Authentication;
using HRM.Modules.Identity.Infrastructure.BackgroundServices;
using HRM.Modules.Identity.Infrastructure.Persistence;
using HRM.Modules.Identity.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Modules.Identity.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection configuration for Identity module Infrastructure layer
/// Registers DbContext, repositories, background services
///
/// Registration:
/// - DbContext: IdentityDbContext (SQL Server, connection string "HrmDb")
/// - Repositories: IOperatorRepository -> OperatorRepository (Scoped)
/// - Authentication Services: IPasswordHasher -> PasswordHasher (Singleton), ITokenService -> TokenService (Singleton)
/// - JWT Options: Configuration from appsettings.json (JwtSettings section)
/// - Background Services: IdentityOutboxProcessor (Singleton, IHostedService)
/// - Interceptors: AuditInterceptor (from BuildingBlocks)
///
/// Service Lifetimes:
/// - DbContext: Scoped (one per HTTP request)
/// - Repositories: Scoped (same lifetime as DbContext)
/// - Background Services: Singleton (runs continuously)
///
/// Configuration:
/// - Connection string: appsettings.json -> ConnectionStrings:HrmDatabase
/// - Same database as other modules (schema separation)
/// - SQL Server provider (Microsoft.EntityFrameworkCore.SqlServer)
///
/// Usage (Startup/Program.cs):
/// <code>
/// // Add BuildingBlocks infrastructure first
/// builder.Services.AddBuildingBlocksInfrastructure(builder.Configuration);
///
/// // Add Identity infrastructure
/// builder.Services.AddIdentityInfrastructure(builder.Configuration);
/// </code>
/// </summary>
public static class IdentityInfrastructureExtensions
{
    /// <summary>
    /// Add Identity module infrastructure services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration (for connection string)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Register DbContext
        // Connection string: "HrmDatabase" from appsettings.json
        // Shared database with other modules (schema separation)
        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("HrmDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'HrmDatabase' not found in configuration. " +
                    "Please add it to appsettings.json: " +
                    "\"ConnectionStrings\": { \"HrmDatabase\": \"Server=...;Database=HrmDb;...\" }");

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                // Enable retry on transient failures (network issues, deadlocks)
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null
                );

                // Set command timeout (30 seconds)
                sqlOptions.CommandTimeout(30);

                // Use SQL Server 2022 features
                // sqlOptions.UseCompatibilityLevel(160);
            });

            // Get AuditInterceptor from DI (registered by BuildingBlocks)
            var auditInterceptor = serviceProvider.GetService<HRM.BuildingBlocks.Infrastructure.Persistence.Interceptors.AuditInterceptor>();
            if (auditInterceptor != null)
            {
                options.AddInterceptors(auditInterceptor);
            }

            // Enable sensitive data logging in Development (careful in Production!)
            // options.EnableSensitiveDataLogging();
            // options.EnableDetailedErrors();
        });

        // Register IUnitOfWork - resolves to IdentityDbContext
        // UnitOfWorkBehavior depends on IUnitOfWork for transaction management
        services.AddScoped<HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork.IUnitOfWork>(
            sp => sp.GetRequiredService<IdentityDbContext>());

        // 2. Register Repositories
        // Scoped: One instance per HTTP request
        services.AddScoped<IOperatorRepository, OperatorRepository>();

        // 3. Register Authentication Services
        // Singleton: Stateless services, safe to share across requests
        // NOTE: These are Identity module-specific, used only for login/registration
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

        // Configure JWT Options from appsettings.json
        // Section: "JwtSettings" with SecretKey, Issuer, Audience, etc.
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName)
        );

        // 4. Register Background Services
        // Singleton: Runs continuously in background
        // IHostedService: Starts automatically with application
        services.AddHostedService<IdentityOutboxProcessor>();

        // 5. Register MediatR handlers from Application layer
        // Discovers all ICommandHandler, IQueryHandler, INotificationHandler
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(
                typeof(Application.Commands.RegisterOperator.RegisterOperatorCommand).Assembly
            );
        });

        // 6. Register FluentValidation validators from Application layer
        // Discovers all AbstractValidator<T>
        services.AddValidatorsFromAssembly(
            typeof(Application.Commands.RegisterOperator.RegisterOperatorCommand).Assembly
        );

        return services;
    }
}
