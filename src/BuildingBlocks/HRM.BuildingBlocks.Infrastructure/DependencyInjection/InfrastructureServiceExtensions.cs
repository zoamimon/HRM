using System.Reflection;
using System.Text;
using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Application.Abstractions.Data;
using HRM.BuildingBlocks.Application.Abstractions.EventBus;
using HRM.BuildingBlocks.Application.Behaviors;
using HRM.BuildingBlocks.Infrastructure.Authentication;
using HRM.BuildingBlocks.Infrastructure.EventBus;
using HRM.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HRM.BuildingBlocks.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering infrastructure services
/// Provides centralized registration for all BuildingBlocks.Infrastructure components
///
/// Usage in Program.cs or Module registration:
/// <code>
/// // Register BuildingBlocks infrastructure services
/// services.AddBuildingBlocksInfrastructure(configuration);
///
/// // Register JWT authentication
/// services.AddJwtAuthentication(configuration);
///
/// // Register module-specific DbContext with interceptors
/// services.AddDbContext<IdentityDbContext>(options =>
/// {
///     options.UseSqlServer(connectionString);
///     options.AddInterceptors(
///         services.BuildServiceProvider().GetRequiredService<AuditInterceptor>()
///     );
/// });
/// </code>
///
/// What Gets Registered:
/// - IEventBus → InMemoryEventBus (singleton)
/// - ICurrentUserService → CurrentUserService (scoped)
/// - AuditInterceptor (singleton)
/// - HttpContextAccessor (for CurrentUserService)
/// - MediatR with pipeline behaviors:
///   * LoggingBehavior (logs all requests/responses)
///   * ValidationBehavior (validates commands/queries with FluentValidation)
///   * UnitOfWorkBehavior (commits UnitOfWork after successful handling)
///
/// Not Registered Here:
/// - Module-specific DbContexts (registered per module)
/// - OutboxProcessor (registered per module as hosted service)
/// - Repositories (registered per module)
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Register all BuildingBlocks infrastructure services
    ///
    /// Services Registered:
    /// - Event Bus (InMemoryEventBus)
    /// - Authentication services (CurrentUserService only - IPasswordHasher/ITokenService moved to Identity module)
    /// - EF Core interceptors (AuditInterceptor)
    /// - HttpContextAccessor
    /// - MediatR with pipeline behaviors (Logging, Validation, UnitOfWork)
    ///
    /// Services NOT Registered (Module Responsibility):
    /// - IDataScopingService: Requires IDbConnection which is module-specific
    /// - IDbConnection: Each module must register with own connection string
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddBuildingBlocksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Event Bus (In-Memory for modular monolith)
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        // HTTP Context Accessor (required for CurrentUserService)
        services.AddHttpContextAccessor();

        // Authentication Services
        // NOTE: ICurrentUserService is shared across all modules for authorization
        // IPasswordHasher and ITokenService are registered in Identity module (authentication-specific)
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Authorization Services
        // NOTE: DataScopingService requires IDbConnection which must be registered at module level
        // Each module should register its own DataScopingService with module-specific IDbConnection:
        // services.AddScoped<IDataScopingService, DataScopingService>();
        // services.AddScoped<IDbConnection>(sp =>
        //     new SqlConnection(configuration.GetConnectionString("ModuleDb")));

        // EF Core Interceptors
        services.AddSingleton<AuditInterceptor>();

        // MediatR with Pipeline Behaviors
        // Register from Application assembly where commands, queries, and behaviors are defined
        services.AddMediatR(config =>
        {
            // Register all handlers from Application layer assembly
            config.RegisterServicesFromAssembly(typeof(ValidationBehavior<,>).Assembly);

            // Register pipeline behaviors (order matters - executes in registration order)
            // 1. Logging: Log all requests/responses
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));

            // 2. Validation: Validate commands/queries before handling
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));

            // 3. Unit of Work: Commit changes after successful handling
            config.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
        });

        return services;
    }

    /// <summary>
    /// Register JWT authentication middleware
    /// Configures JWT bearer authentication with validation parameters
    ///
    /// Configuration Required (appsettings.json):
    /// <code>
    /// {
    ///   "JwtSettings": {
    ///     "SecretKey": "your-256-bit-secret-key-min-32-characters",
    ///     "Issuer": "HRM.Api",
    ///     "Audience": "HRM.Clients",
    ///     "AccessTokenExpiryMinutes": 15,
    ///     "RefreshTokenExpiryDays": 7
    ///   }
    /// }
    /// </code>
    ///
    /// Usage in Program.cs:
    /// <code>
    /// // Register authentication
    /// services.AddJwtAuthentication(configuration);
    ///
    /// // Use authentication middleware (before authorization)
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// </code>
    ///
    /// Token Validation:
    /// - Validates signature using secret key
    /// - Validates issuer matches configuration
    /// - Validates audience matches configuration
    /// - Validates token not expired
    /// - Validates token lifetime
    ///
    /// Claims:
    /// - Populates HttpContext.User.Claims from JWT
    /// - CurrentUserService reads from HttpContext.User
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get JWT settings from configuration
        var jwtSettings = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        if (jwtSettings is null)
        {
            throw new InvalidOperationException(
                $"JWT settings not found in configuration. " +
                $"Ensure '{JwtOptions.SectionName}' section exists in appsettings.json"
            );
        }

        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
        {
            throw new InvalidOperationException(
                "JWT SecretKey is not configured. " +
                "Set JwtSettings:SecretKey in appsettings.json or environment variables."
            );
        }

        if (jwtSettings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT SecretKey must be at least 32 characters (256 bits) for HMAC-SHA256. " +
                $"Current length: {jwtSettings.SecretKey.Length}"
            );
        }

        // Configure JWT Bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true; // Enforce HTTPS in production
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                // Validate signature
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)
                ),

                // Validate issuer
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                // Validate audience
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                // Validate token expiration
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero // No clock skew tolerance
            };

            // Configure event handlers
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    // Log authentication failures
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<Microsoft.Extensions.Logging.ILogger<JwtBearerEvents>>();

                    logger.LogWarning(
                        context.Exception,
                        "JWT authentication failed: {Reason}",
                        context.Exception.Message
                    );

                    return Task.CompletedTask;
                },

                OnTokenValidated = context =>
                {
                    // Optional: Additional validation logic
                    // Example: Check if user is active in database
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Helper method to get EF Core interceptor for DbContext configuration
    /// Use this when registering module DbContexts
    ///
    /// Usage:
    /// <code>
    /// services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
    /// {
    ///     options.UseSqlServer(connectionString);
    ///     options.AddInterceptors(serviceProvider.GetAuditInterceptor());
    /// });
    /// </code>
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>AuditInterceptor instance</returns>
    public static ISaveChangesInterceptor GetAuditInterceptor(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<AuditInterceptor>();
    }
}
