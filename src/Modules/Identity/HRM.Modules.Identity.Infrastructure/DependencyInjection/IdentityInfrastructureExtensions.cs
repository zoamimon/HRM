using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using HRM.BuildingBlocks.Domain.Abstractions.Permissions;
using HRM.Modules.Identity.Application;
using HRM.Modules.Identity.Application.Abstractions.Authentication;
using HRM.Modules.Identity.Application.Configuration;
using HRM.Modules.Identity.Domain.Repositories;
using HRM.Modules.Identity.Domain.Services;
using HRM.Modules.Identity.Infrastructure.Authentication;
using HRM.Modules.Identity.Infrastructure.BackgroundServices;
using HRM.Modules.Identity.Infrastructure.Persistence;
using HRM.Modules.Identity.Infrastructure.Persistence.Repositories;
using HRM.Modules.Identity.Infrastructure.Services;
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

        // Register IModuleUnitOfWork - resolves to IdentityDbContext
        // UnitOfWorkBehavior depends on IModuleUnitOfWork for transaction management
        // ModuleName property returns "Identity" for routing
        services.AddScoped<HRM.BuildingBlocks.Domain.Abstractions.UnitOfWork.IModuleUnitOfWork>(
            sp => sp.GetRequiredService<IdentityDbContext>());

        // 2. Register Repositories
        // Scoped: One instance per HTTP request
        services.AddScoped<IOperatorRepository, OperatorRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Singleton: Dapper-based repository for permission queries (uses connection string directly)
        services.AddSingleton<IOperatorPermissionRepository, OperatorPermissionRepository>();

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

        // 5. Register Permission Catalog Services
        // MemoryCache: Required for caching parsed catalog
        services.AddMemoryCache();

        // Factory: Creates IPermissionCatalogSource instances for modules
        // Service: Aggregates all sources and provides catalog access
        services.AddSingleton<IPermissionCatalogSourceFactory, PermissionCatalogSourceFactory>();
        services.AddSingleton<IPermissionCatalogService, PermissionCatalogService>();

        // Register Identity module's catalog source (from embedded resource)
        // Each module registers its own catalog source using the factory
        // Resource name: {Namespace}.Resources.PermissionCatalog.xml
        services.AddSingleton<IPermissionCatalogSource>(sp =>
        {
            var factory = sp.GetRequiredService<IPermissionCatalogSourceFactory>();
            return factory.FromEmbeddedResource(
                typeof(IdentityApplicationAssemblyMarker).Assembly,
                "HRM.Modules.Identity.Application.Resources.PermissionCatalog.xml");
        });

        // 6. Register Authorization Services
        // IPermissionService: Checks user permissions for authorization
        // Scoped: Uses scoped repositories for database access
        services.AddScoped<IPermissionService, PermissionService>();

        // 7. Register MediatR handlers from Infrastructure assembly
        // Query handlers that need direct DbContext access are located here
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(
                typeof(IdentityInfrastructureExtensions).Assembly
            );
        });

        return services;
    }
}
