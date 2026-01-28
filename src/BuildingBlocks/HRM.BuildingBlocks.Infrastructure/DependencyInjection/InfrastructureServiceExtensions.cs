using System.Reflection;
using System.Text;
using HRM.BuildingBlocks.Application.Abstractions.Authentication;
using HRM.BuildingBlocks.Application.Abstractions.Data;
using HRM.BuildingBlocks.Application.Abstractions.EventBus;
using HRM.BuildingBlocks.Application.Abstractions.Infrastructure;
using HRM.BuildingBlocks.Application.Abstractions.Authorization;
using HRM.BuildingBlocks.Domain.Abstractions.Security;
using HRM.BuildingBlocks.Infrastructure.Authentication;
using HRM.BuildingBlocks.Infrastructure.EventBus;
using HRM.BuildingBlocks.Infrastructure.Http;
using HRM.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using HRM.BuildingBlocks.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
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
/// - IClientInfoService → ClientInfoService (scoped)
/// - IClaimsTransformation → RolesClaimsTransformation (scoped)
/// - AuditInterceptor (scoped - depends on ICurrentUserService)
/// - HttpContextAccessor (for CurrentUserService and ClientInfoService)
///
/// NOT Registered Here (moved to Application layer):
/// - MediatR (registered in BuildingBlocksApplication)
/// - Pipeline behaviors (registered in BuildingBlocksApplication)
///
/// Not Registered Here (module-specific):
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
    /// - Authentication services (CurrentUserService, ClientInfoService)
    /// - EF Core interceptors (AuditInterceptor)
    /// - HttpContextAccessor
    /// - Claims transformation (RolesClaimsTransformation)
    ///
    /// Services NOT Registered (Module Responsibility):
    /// - IDataScopingService: Requires IDbConnection which is module-specific
    /// - IDbConnection: Each module must register with own connection string
    /// - IPasswordHasher/ITokenService: Moved to Identity module (authentication-specific)
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

        // HTTP Context Services
        // NOTE: IClientInfoService provides access to HTTP request context (IP, UserAgent, etc.)
        // Used for audit logging, session management, and security tracking
        // Abstracts away HttpContext from application layer (Clean Architecture)
        services.AddScoped<IClientInfoService, ClientInfoService>();

        // Claims Transformation
        // Normalizes role claims from various formats (comma-separated, multiple claims, etc.)
        // into standard ClaimTypes.Role claims for native ASP.NET Core authorization support
        services.AddScoped<IClaimsTransformation, RolesClaimsTransformation>();

        // Authorization Services
        // NOTE: DataScopingService requires IDbConnection which must be registered at module level
        // Each module should register its own DataScopingService with module-specific IDbConnection:
        // services.AddScoped<IDataScopingService, DataScopingService>();
        // services.AddScoped<IDbConnection>(sp =>
        //     new SqlConnection(configuration.GetConnectionString("ModuleDb")));

        // EF Core Interceptors
        // NOTE: AuditInterceptor is Scoped because it depends on ICurrentUserService (Scoped)
        services.AddScoped<AuditInterceptor>();

        // Route-based Security Services
        // Singleton: RouteSecurityService maintains route security map in memory
        // Modules register their RouteSecurityMap.xml via LoadFromEmbeddedResource
        services.AddSingleton<IRouteSecurityService, RouteSecurityService>();

        // Scoped: PermissionFilterService resolves IPermissionQueryFilter<T> from DI
        // Used for data-level security filtering based on user's permission scope
        services.AddScoped<IPermissionFilterService, PermissionFilterService>();

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
        var jwtSection = configuration.GetSection("JwtSettings");
        var secretKey = jwtSection["SecretKey"];
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "JWT SecretKey is not configured. " +
                "Set JwtSettings:SecretKey in appsettings.json or environment variables."
            );
        }

        if (secretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT SecretKey must be at least 32 characters (256 bits) for HMAC-SHA256. " +
                $"Current length: {secretKey.Length}"
            );
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException(
                "JWT Issuer is not configured in JwtSettings:Issuer"
            );
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "JWT Audience is not configured in JwtSettings:Audience"
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
                    Encoding.UTF8.GetBytes(secretKey)
                ),

                // Validate issuer
                ValidateIssuer = true,
                ValidIssuer = issuer,

                // Validate audience
                ValidateAudience = true,
                ValidAudience = audience,

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
