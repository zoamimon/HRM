# HRM.BuildingBlocks.Infrastructure

Infrastructure layer implementation for the HRM (Human Resource Management) system.

## Overview

This project provides infrastructure implementations for all BuildingBlocks abstractions defined in:
- `HRM.BuildingBlocks.Domain`
- `HRM.BuildingBlocks.Application`

## Project Structure

```
HRM.BuildingBlocks.Infrastructure/
├── Persistence/
│   ├── ModuleDbContext.cs              # Base DbContext with UnitOfWork
│   ├── Configurations/
│   │   └── OutboxMessageConfiguration.cs
│   ├── Interceptors/
│   │   └── AuditInterceptor.cs        # Auto-update ModifiedAtUtc
│   └── Repositories/
│       └── (GenericRepository - future)
│
├── EventBus/
│   └── InMemoryEventBus.cs            # MediatR-based event bus
│
├── BackgroundServices/
│   └── OutboxProcessor.cs             # Process OutboxMessages
│
├── Authentication/
│   ├── CurrentUserService.cs          # Read JWT claims
│   ├── PasswordHasher.cs              # BCrypt password hashing
│   ├── TokenService.cs                # JWT token generation
│   └── JwtOptions.cs                  # JWT configuration
│
├── Authorization/
│   └── DataScopingService.cs          # Data scoping filters
│
└── DependencyInjection/
    └── InfrastructureServiceExtensions.cs  # DI registration
```

## Key Components

### 1. Persistence

#### **ModuleDbContext**
Base class for all module DbContexts. Implements:
- `IUnitOfWork.CommitAsync()` with domain event dispatch
- `IModuleContext.SaveChangesAsync()`
- Automatic audit field updates
- OutboxMessage configuration

Usage:
```csharp
public class IdentityDbContext : ModuleDbContext
{
    public override string ModuleName => "Identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, IPublisher publisher)
        : base(options, publisher)
    {
    }
}
```

#### **AuditInterceptor**
EF Core interceptor that automatically updates `ModifiedAtUtc` on entity changes.

#### **OutboxMessageConfiguration**
Entity configuration for OutboxMessage with optimized indexes for processing.

### 2. Event Bus

#### **InMemoryEventBus**
In-memory event bus implementation using MediatR for modular monolith architecture.

Features:
- Fast (in-process, no network)
- Thread-safe
- Suitable for modular monolith
- Easy migration path to RabbitMQ

### 3. Background Services

#### **OutboxProcessor**
Background service that processes unprocessed OutboxMessages.

Configuration:
- Polling interval: 1 minute (60 seconds)
- Batch size: 100 messages
- Max retry attempts: 5
- Processes messages in chronological order

Usage:
```csharp
public class IdentityOutboxProcessor : OutboxProcessor
{
    public IdentityOutboxProcessor(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<IdentityOutboxProcessor> logger)
        : base(serviceScopeFactory, logger)
    {
    }

    protected override DbContext GetDbContext(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IdentityDbContext>();
    }
}

// Register in Program.cs
services.AddHostedService<IdentityOutboxProcessor>();
```

### 4. Authentication

#### **CurrentUserService**
Reads user information from JWT claims in HttpContext.User.

Claims:
- `sub` → UserId
- `name` → Username
- `email` → Email
- `UserType` → Operator or User
- `ScopeLevel` → Company/Department/Position/Employee
- `EmployeeId` → Employee identifier (Users only)
- `Roles` → Comma-separated roles

#### **PasswordHasher**
BCrypt-based password hashing with:
- Work factor: 12 (2^12 = 4096 iterations)
- Automatic salt generation
- Constant-time verification
- ~150ms per operation

#### **TokenService**
JWT token generation:
- Access token: 15 minutes (configurable)
- Refresh token: 7 days (configurable)
- HMAC-SHA256 signature
- Includes user claims for authorization

#### **JwtOptions**
Configuration class for JWT settings from appsettings.json.

### 5. Authorization

#### **DataScopingService**
Applies data scoping filters based on user's scope level.

Scope Levels:
- **Operator**: Global access (no filtering)
- **Company**: Filter by assigned companies
- **Department**: Filter by assigned departments
- **Position**: Filter by assigned positions
- **Employee**: Filter to own data only

Caching:
- Per-request caching (Scoped lifetime)
- Single database query per request
- Reused across multiple query handlers

### 6. Dependency Injection

#### **InfrastructureServiceExtensions**
Extension methods for registering all infrastructure services.

Usage in Program.cs:
```csharp
// Register all BuildingBlocks infrastructure
services.AddBuildingBlocksInfrastructure(configuration);

// Register JWT authentication
services.AddJwtAuthentication(configuration);

// Register module DbContext with interceptors
services.AddDbContext<IdentityDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(sp.GetAuditInterceptor());
});
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "HrmDatabase": "Server=localhost;Database=HrmDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-min-32-characters-long-for-hs256",
    "Issuer": "HRM.Api",
    "Audience": "HRM.Clients",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

### Environment Variables (Production)

```bash
export ConnectionStrings__HrmDatabase="Server=prod-server;..."
export JwtSettings__SecretKey="production-secret-key-from-key-vault"
```

## Dependencies

- **Microsoft.EntityFrameworkCore** (9.0.0)
- **Microsoft.EntityFrameworkCore.SqlServer** (9.0.0)
- **Microsoft.EntityFrameworkCore.Relational** (9.0.0)
- **Dapper** (2.1.66)
- **Microsoft.AspNetCore.Authentication.JwtBearer** (9.0.0)
- **System.IdentityModel.Tokens.Jwt** (8.3.0)
- **BCrypt.Net-Next** (4.0.3)
- **MediatR** (14.0.0)
- **Microsoft.Extensions.Caching.Memory** (9.0.0)

## Design Patterns Used

1. **Unit of Work Pattern**: `ModuleDbContext` implements `IUnitOfWork`
2. **Repository Pattern**: Base repository support (can be extended)
3. **Transactional Outbox Pattern**: `OutboxProcessor` + `OutboxMessage`
4. **Dependency Injection**: All services registered via DI
5. **Options Pattern**: `JwtOptions` configured via IOptions
6. **Interceptor Pattern**: `AuditInterceptor` for cross-cutting concerns
7. **Factory Pattern**: `OutboxMessage.Create()` for entity creation
8. **Strategy Pattern**: Different data scoping strategies per level
9. **Background Service Pattern**: `OutboxProcessor` for async processing

## Security Considerations

### Password Security
- BCrypt with work factor 12
- Automatic salt generation
- Never store plaintext passwords
- Never log passwords or hashes

### JWT Security
- Secret key min 256 bits (32 characters)
- Store secret in environment variables or key vault
- HTTPS required in production
- Short-lived access tokens (15 minutes)
- Long-lived but revocable refresh tokens (7 days)

### Data Scoping Security
- Always applied server-side
- Cannot be bypassed by client
- Operators have global access
- Users restricted by ScopeLevel
- Empty results on scope violation (not errors)

## Performance Optimizations

1. **Request-scoped caching**: DataScopeContext cached per request
2. **Batch processing**: OutboxProcessor processes 100 messages at a time
3. **Indexed queries**: Optimized indexes on OutboxMessages
4. **Lazy loading**: Scope context loaded only when needed
5. **Connection pooling**: SQL Server connection pool enabled
6. **Async everywhere**: All I/O operations are async

## Monitoring and Logging

### Log Levels
- **Information**: Successful operations, message processing
- **Warning**: Retryable failures, dead letter messages
- **Error**: Permanent failures, configuration issues

### Key Metrics to Monitor
- OutboxMessages processed per minute
- Failed message rate
- Dead letter queue size
- Average processing time
- Database query performance
- JWT authentication failures

## Testing Strategy

### Unit Tests
- Test each service in isolation
- Mock dependencies (IDbConnection, IHttpContextAccessor)
- Test edge cases and error handling

### Integration Tests
- Test with real database (SQL Server in container)
- Test OutboxProcessor end-to-end
- Test JWT authentication flow
- Test data scoping queries

### Performance Tests
- Load test OutboxProcessor with 1000s of messages
- Stress test data scoping with large datasets
- Benchmark password hashing performance

## Migration from Modular Monolith to Microservices

When ready to migrate:

1. **Replace InMemoryEventBus with RabbitMqEventBus**
   - Implement `RabbitMqEventBus : IEventBus`
   - Same interface, different implementation
   - No changes to application code

2. **Deploy separate OutboxProcessor per service**
   - Each service has own OutboxMessages table
   - Each publishes to RabbitMQ independently

3. **Use distributed tracing**
   - Add correlation IDs to events
   - Track events across services

4. **Consider eventual consistency**
   - Handle out-of-order events
   - Implement idempotent handlers

## Future Enhancements

1. **Distributed Locking** for OutboxProcessor (Redis, SQL Server locks)
2. **Event Versioning** support for backward compatibility
3. **Dead Letter Queue UI** for manual intervention
4. **Metrics Dashboard** for monitoring
5. **Rate Limiting** for token generation
6. **Two-Factor Authentication** support
7. **Argon2 Password Hashing** option (more secure than BCrypt)
8. **RabbitMQ Event Bus** implementation

## License

Internal - Company Proprietary

## Authors

- Infrastructure Team
- Date: 2026-01-15
