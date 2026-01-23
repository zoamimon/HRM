# UnitOfWorkBehavior Debug Guide

## Problem
User reports: UnitOfWorkBehavior không gọi CommitAsync() khi login.

## Diagnostic Checklist

### 1. Verify UnitOfWorkBehavior is Registered

**Check**: `ApplicationServiceExtensions.cs` line 60
```csharp
config.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
```

**Status**: ✅ Confirmed registered in pipeline

### 2. Verify LoginCommand Implements IModuleCommand

**Check**: `LoginCommand.cs` line 85
```csharp
public sealed record LoginCommand(...) : IModuleCommand<LoginResponse>
{
    public string ModuleName => "Identity";
}
```

**Status**: ✅ Confirmed implements IModuleCommand<LoginResponse>

### 3. Verify Handler Return Type

**Check**: `LoginCommandHandler.cs` line 42-43
```csharp
public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
```

**Expected**: IRequestHandler<LoginCommand, Result<LoginResponse>>
**Status**: ✅ Correct type signature

### 4. Verify IModuleUnitOfWork Registration

**Check**: `IdentityInfrastructureExtensions.cs` line 99-100
```csharp
services.AddScoped<IModuleUnitOfWork>(
    sp => sp.GetRequiredService<IdentityDbContext>());
```

**Status**: ✅ Confirmed registered

### 5. Check Module Registration Order

**Check**: `ModuleExtensions.cs` lines 55, 71, 80
```csharp
services.AddBuildingBlocksApplication();  // Line 55 - Registers UnitOfWorkBehavior
services.AddIdentityApplication();        // Line 71 - Registers LoginCommandHandler
services.AddIdentityInfrastructure(configuration);  // Line 80 - Registers IModuleUnitOfWork
```

**Status**: ✅ Correct order

## Possible Causes

### Cause 1: Generic Type Constraint Mismatch

**UnitOfWorkBehavior constraint**:
```csharp
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IModuleCommand<TResponse>
```

**LoginCommand type**:
```csharp
LoginCommand : IModuleCommand<LoginResponse>
```

**Handler type**:
```csharp
IRequestHandler<LoginCommand, Result<LoginResponse>>
```

**Issue**: UnitOfWorkBehavior expects `TRequest : IModuleCommand<TResponse>` where TResponse is `LoginResponse`, BUT MediatR pipeline uses `Result<LoginResponse>` as the response type!

**This means**:
- MediatR pipeline: `IPipelineBehavior<LoginCommand, Result<LoginResponse>>`
- UnitOfWorkBehavior constraint: `TRequest : IModuleCommand<TResponse>` where TResponse = `Result<LoginResponse>` ❌

**But LoginCommand is**:
- `IModuleCommand<LoginResponse>` (NOT `IModuleCommand<Result<LoginResponse>>`)

### The Root Cause

MediatR resolves behaviors based on the handler's return type: `Result<LoginResponse>`

UnitOfWorkBehavior constraint is:
```csharp
where TRequest : IModuleCommand<TResponse>
```

When MediatR tries to instantiate:
```csharp
UnitOfWorkBehavior<LoginCommand, Result<LoginResponse>>
```

It checks if `LoginCommand : IModuleCommand<Result<LoginResponse>>`

But LoginCommand is: `IModuleCommand<LoginResponse>` ❌

**Type mismatch!** UnitOfWorkBehavior is NEVER instantiated for this command!

## The Fix

### Option 1: Change ICommand Interface (RECOMMENDED)

**Current** (BuildingBlocks/Application/Abstractions/Commands/ICommand.cs):
```csharp
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase
```

**Problem**: ICommand<LoginResponse> means IRequest<Result<LoginResponse>>

**Solution**: IModuleCommand should expose UNWRAPPED response type

**Change IModuleCommand**:
```csharp
// BEFORE (wrong)
public interface IModuleCommand<TResponse> : ICommand<TResponse>
{
    string ModuleName { get; }
}

// AFTER (correct) - bypass ICommand wrapper
public interface IModuleCommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase
{
    string ModuleName { get; }
}
```

This won't work because ICommand already wraps in Result.

### Option 2: Change UnitOfWorkBehavior Constraint (RECOMMENDED) ⭐

**Current**:
```csharp
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IModuleCommand<TResponse>  // ❌ Wrong - TResponse is Result<T>
```

**Fixed**:
```csharp
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommandBase  // ✅ Correct - just check if it's a command
```

**Then check ModuleName at runtime**:
```csharp
public async Task<TResponse> Handle(...)
{
    var response = await next();

    // Check if command has ModuleName property (IModuleCommand)
    if (request is not IHasModuleName moduleCommand)
        return response;  // Not a module command, skip UoW

    // Resolve module-specific UnitOfWork
    var unitOfWork = _unitOfWorks.SingleOrDefault(x => x.ModuleName == moduleCommand.ModuleName)
        ?? throw new InvalidOperationException(...);

    await unitOfWork.CommitAsync(cancellationToken);
    return response;
}
```

**Add marker interface**:
```csharp
public interface IHasModuleName
{
    string ModuleName { get; }
}

public interface IModuleCommand<TResponse> : ICommand<TResponse>, IHasModuleName
{
    // ModuleName already required by IHasModuleName
}
```

## Verification Steps

### Step 1: Add Logging to UnitOfWorkBehavior

**Edit**: `UnitOfWorkBehavior.cs`

```csharp
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    Console.WriteLine($"🔵 UnitOfWorkBehavior.Handle - Request: {typeof(TRequest).Name}, Response: {typeof(TResponse).Name}");
    Console.WriteLine($"🔵 Request is IModuleCommand<TResponse>: {request is IModuleCommand<TResponse>}");

    var response = await next();

    Console.WriteLine($"🔵 Handler completed, resolving UnitOfWork...");

    var unitOfWork = _unitOfWorks.SingleOrDefault(x => x.ModuleName == request.ModuleName)
        ?? throw new InvalidOperationException(...);

    Console.WriteLine($"🔵 Calling CommitAsync on {unitOfWork.GetType().Name}...");
    await unitOfWork.CommitAsync(cancellationToken);
    Console.WriteLine($"🔵 CommitAsync completed successfully");

    return response;
}
```

### Step 2: Test Login

```bash
dotnet run --project src/Apps/HRM.Api

curl -X POST http://localhost:5001/api/identity/login \
  -H "Content-Type: application/json" \
  -d '{"usernameOrEmail":"admin","password":"Admin@123456"}'
```

**Expected Output** (if UnitOfWorkBehavior runs):
```
🔵 UnitOfWorkBehavior.Handle - Request: LoginCommand, Response: Result<LoginResponse>
🔵 Request is IModuleCommand<TResponse>: ???  ← This will show if constraint matches
...
```

**If you see NOTHING**: UnitOfWorkBehavior is NOT being instantiated due to type constraint mismatch!

### Step 3: Check All Registered Behaviors

Add to Program.cs after `builder.Services.AddModules(...)`:

```csharp
// Debug: Print all registered IPipelineBehavior services
var pipelineBehaviors = builder.Services
    .Where(sd => sd.ServiceType.IsGenericType &&
                 sd.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
    .ToList();

Console.WriteLine($"📋 Registered Pipeline Behaviors: {pipelineBehaviors.Count}");
foreach (var behavior in pipelineBehaviors)
{
    Console.WriteLine($"  - {behavior.ImplementationType?.Name ?? behavior.ServiceType.Name}");
}
```

## Temporary Workaround

If you need refresh token to work NOW while fixing the behavior:

### Option A: Manual CommitAsync in Handler

**Edit**: `LoginCommandHandler.cs` (line 140 after Add)

```csharp
_refreshTokenRepository.Add(refreshTokenEntity);

// TEMPORARY: Manual commit until UnitOfWorkBehavior is fixed
await _unitOfWork.CommitAsync(cancellationToken);  // Add this line
```

**Inject IModuleUnitOfWork**:
```csharp
public LoginCommandHandler(
    IOperatorRepository operatorRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions,
    IRefreshTokenRepository refreshTokenRepository,
    IModuleUnitOfWork unitOfWork)  // Add this
{
    _operatorRepository = operatorRepository;
    _passwordHasher = passwordHasher;
    _tokenService = tokenService;
    _jwtOptions = jwtOptions.Value;
    _refreshTokenRepository = refreshTokenRepository;
    _unitOfWork = unitOfWork;  // Add this
}

private readonly IModuleUnitOfWork _unitOfWork;  // Add this field
```

### Option B: Call SaveChangesAsync Directly

```csharp
_refreshTokenRepository.Add(refreshTokenEntity);

// TEMPORARY: Direct SaveChanges
var dbContext = (IdentityDbContext)_refreshTokenRepository.GetType()
    .GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)!
    .GetValue(_refreshTokenRepository)!;

await dbContext.SaveChangesAsync(cancellationToken);
```

## Next Steps

1. ✅ Add logging to verify if UnitOfWorkBehavior is called
2. ✅ Check console output for type constraint match
3. ✅ If not called, implement Option 2 fix (change constraint to ICommandBase + runtime check)
4. ✅ Test login after fix
5. ✅ Verify refresh token is saved to database
6. ✅ Remove temporary workaround if used
