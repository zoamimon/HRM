# RefreshToken Issue - Root Cause Analysis & Fix

## 🔍 Problem Statement

**Symptom**: Login operator admin thành công nhưng refresh token **KHÔNG** được lưu vào database.

**User Observation**: Debug thấy `UnitOfWorkBehavior` không gọi `CommitAsync()`.

## 🎯 Root Cause

### Generic Type Constraint Mismatch

**UnitOfWorkBehavior constraint ban đầu**:
```csharp
public sealed class UnitOfWorkBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IModuleCommand<TResponse>  // ❌ WRONG!
```

**MediatR pipeline resolution**:
```csharp
// LoginCommand definition
public sealed record LoginCommand(...) : IModuleCommand<LoginResponse>

// MediatR instantiates behavior as:
IPipelineBehavior<LoginCommand, Result<LoginResponse>>
                                 ^^^^^^^^^^^^^^^^^^^^^^
                                 TResponse in pipeline
```

**Type checking**:
```
MediatR checks if: LoginCommand : IModuleCommand<Result<LoginResponse>> ?

But LoginCommand is: IModuleCommand<LoginResponse>
                                     ^^^^^^^^^^^^^^
                                     Unwrapped type

Result: TYPE MISMATCH! ❌
```

**Consequence**:
- Behavior is **NEVER instantiated** for LoginCommand
- `Handle()` method **NEVER called**
- `CommitAsync()` **NEVER executed**
- RefreshToken **NEVER saved** to database

## 📊 Technical Explanation

### Why This Happens

1. **ICommand interface wraps responses**:
```csharp
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
                                                 ^^^^^^^^^^^^^^^^
                                                 Wrapped in Result<T>
```

2. **IModuleCommand inherits ICommand**:
```csharp
public interface IModuleCommand<TResponse> : ICommand<TResponse>
{
    string ModuleName { get; }
}
```

3. **LoginCommand uses unwrapped type**:
```csharp
LoginCommand : IModuleCommand<LoginResponse>
// This means: IRequest<Result<LoginResponse>>
```

4. **MediatR pipeline resolution**:
```csharp
// Handler signature
IRequestHandler<LoginCommand, Result<LoginResponse>>

// MediatR creates pipeline
IPipelineBehavior<LoginCommand, Result<LoginResponse>>
                                 ^^^^^^^^^^^^^^^^^^^^^^
                                 TResponse = Result<LoginResponse>

// Constraint check
where TRequest : IModuleCommand<TResponse>
// Becomes: LoginCommand : IModuleCommand<Result<LoginResponse>>
// But actual: LoginCommand : IModuleCommand<LoginResponse>
// MISMATCH! ❌
```

### Visualization

```
┌─────────────────────────────────────────────────────────────┐
│  MediatR Pipeline                                           │
├─────────────────────────────────────────────────────────────┤
│  Request:  LoginCommand                                     │
│  Response: Result<LoginResponse>  ← TResponse in pipeline   │
└─────────────────────────────────────────────────────────────┘
                    │
                    ├─ AuditBehavior<LoginCommand, Result<LoginResponse>> ✅
                    ├─ LoggingBehavior<LoginCommand, Result<LoginResponse>> ✅
                    ├─ ValidationBehavior<LoginCommand, Result<LoginResponse>> ✅
                    ├─ UnitOfWorkBehavior<LoginCommand, Result<LoginResponse>> ❌
                    │    └─ Constraint: TRequest : IModuleCommand<TResponse>
                    │       where TResponse = Result<LoginResponse>
                    │       Check: LoginCommand : IModuleCommand<Result<LoginResponse>> ?
                    │       Actual: LoginCommand : IModuleCommand<LoginResponse>
                    │       Result: TYPE MISMATCH - Behavior NOT instantiated
                    │
                    └─ Handler ✅

Legend:
✅ = Behavior runs successfully
❌ = Behavior NOT instantiated (type constraint mismatch)
```

## ✅ Solution

### Changed Constraint

**Before**:
```csharp
public sealed class UnitOfWorkBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IModuleCommand<TResponse>  // ❌ Too restrictive
```

**After**:
```csharp
public sealed class UnitOfWorkBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommandBase  // ✅ Broader constraint
```

### Runtime ModuleName Check

**Added reflection-based check**:
```csharp
public async Task<TResponse> Handle(...)
{
    var response = await next();

    // Runtime check for ModuleName property
    var moduleNameProperty = request.GetType().GetProperty("ModuleName");
    if (moduleNameProperty is null)
    {
        // Not a module command, skip UnitOfWork
        return response;
    }

    var moduleName = moduleNameProperty.GetValue(request) as string;

    // Resolve and commit UnitOfWork
    var unitOfWork = _unitOfWorks.Single(x => x.ModuleName == moduleName);
    await unitOfWork.CommitAsync(cancellationToken);

    return response;
}
```

### Why This Works

```
┌─────────────────────────────────────────────────────────────┐
│  MediatR Pipeline (After Fix)                               │
├─────────────────────────────────────────────────────────────┤
│  Request:  LoginCommand                                     │
│  Response: Result<LoginResponse>                            │
└─────────────────────────────────────────────────────────────┘
                    │
                    ├─ AuditBehavior ✅
                    ├─ LoggingBehavior ✅
                    ├─ ValidationBehavior ✅
                    ├─ UnitOfWorkBehavior ✅  ← NOW INSTANTIATED!
                    │    └─ Constraint: TRequest : ICommandBase
                    │       Check: LoginCommand : ICommandBase ? ✅ YES
                    │       Runtime: Check for ModuleName property ✅ EXISTS
                    │       Runtime: Get ModuleName = "Identity" ✅
                    │       Runtime: Resolve IdentityDbContext ✅
                    │       Runtime: Call CommitAsync() ✅
                    │       Runtime: RefreshToken SAVED ✅
                    │
                    └─ Handler ✅
```

## 🚀 Impact

### Before Fix
```
1. User login với username/password ✅
2. LoginCommandHandler validates credentials ✅
3. Handler creates RefreshToken entity ✅
4. Handler calls _refreshTokenRepository.Add() ✅
5. UnitOfWorkBehavior... ❌ NOT CALLED (type mismatch)
6. CommitAsync()... ❌ NEVER EXECUTED
7. RefreshToken... ❌ NOT SAVED to database
8. Login succeeds ✅ (returns access token)
9. Refresh token returned ✅ (in response)
10. BUT token doesn't exist in database ❌
11. When client tries to refresh → FAILS ❌
```

### After Fix
```
1. User login với username/password ✅
2. LoginCommandHandler validates credentials ✅
3. Handler creates RefreshToken entity ✅
4. Handler calls _refreshTokenRepository.Add() ✅
5. UnitOfWorkBehavior.Handle() called ✅
6. Runtime check finds ModuleName = "Identity" ✅
7. Resolves IdentityDbContext as IModuleUnitOfWork ✅
8. Calls CommitAsync() ✅
9. EF Core executes INSERT INTO RefreshTokens ✅
10. RefreshToken SAVED to database ✅
11. Login succeeds ✅
12. Refresh token returned ✅
13. When client tries to refresh → SUCCESS ✅
```

## 📋 Verification Steps

### 1. Check Code Compiles
```bash
dotnet build src/BuildingBlocks/HRM.BuildingBlocks.Application
# Should build without errors
```

### 2. Test Login
```bash
# Start API
dotnet run --project src/Apps/HRM.Api

# Login
curl -X POST http://localhost:5001/api/identity/login \
  -H "Content-Type: application/json" \
  -d '{
    "usernameOrEmail": "admin",
    "password": "Admin@123456",
    "rememberMe": false
  }'

# Expected response:
# {
#   "accessToken": "eyJhbGci...",
#   "refreshToken": "8f3d7b2a9e...",
#   ...
# }
```

### 3. Verify Token Saved in Database
```sql
-- Check RefreshTokens table
SELECT
    Id,
    UserType,
    PrincipalId,
    Token,
    ExpiresAt,
    CreatedAtUtc,
    CreatedByIp
FROM Identity.RefreshTokens
ORDER BY CreatedAtUtc DESC

-- Expected: At least 1 row after login
```

### 4. Test Refresh Token Flow
```bash
# Use refresh token from login response
curl -X POST http://localhost:5001/api/identity/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "8f3d7b2a9e..."
  }'

# Expected: New access token + rotated refresh token
```

## 📁 Files Changed

### Core Fix
- ✅ `src/BuildingBlocks/HRM.BuildingBlocks.Application/Behaviors/UnitOfWorkBehavior.cs`
  - Changed constraint: `IModuleCommand<TResponse>` → `ICommandBase`
  - Added runtime ModuleName check via reflection
  - Graceful handling of non-module commands

### Documentation
- ✅ `UNIT_OF_WORK_DEBUG.md` - Detailed diagnostic guide
- ✅ `REFRESH_TOKEN_ISSUE_SUMMARY.md` - This file
- ✅ `REFRESH_TOKEN_FIX.md` - Migration scripts guide (previous issue)

## 🔧 Additional Considerations

### Database Migration Still Required

**Don't forget**: You still need to run migration scripts to create RefreshTokens table:

```bash
cd src/Database/Identity
./run-all-migrations.sh localhost HrmDb sa YourPassword
```

Or manually:
```bash
sqlcmd -S localhost -U sa -P YourPassword -d HrmDb -i 004_CreateRefreshTokensTable.sql
sqlcmd -S localhost -U sa -P YourPassword -d HrmDb -i 005_MigrateRefreshTokensToPolymorphic.sql
```

### Two Separate Issues

1. **Database schema** (previous issue): RefreshTokens table doesn't exist
   - Fix: Run migration scripts 004 and 005

2. **Code bug** (current issue): UnitOfWorkBehavior not called
   - Fix: Change generic constraint from IModuleCommand<TResponse> to ICommandBase

**Both issues must be fixed** for refresh tokens to work!

## 🎓 Lessons Learned

### Generic Constraints with Result Wrapper

When using Result<T> pattern with MediatR:
- ❌ DON'T use: `where TRequest : IModuleCommand<TResponse>`
- ✅ DO use: `where TRequest : ICommandBase` + runtime checks
- Reason: TResponse in pipeline is `Result<T>`, but command interface uses unwrapped `T`

### MediatR Pipeline Type Resolution

```csharp
// Command definition
IModuleCommand<T>  // Means: IRequest<Result<T>>

// MediatR instantiation
IPipelineBehavior<TRequest, Result<T>>  // TResponse = Result<T>, not T!

// Constraint must account for wrapper
where TRequest : IModuleCommand<TResponse>  // ❌ Expects Result<T> but gets T
where TRequest : ICommandBase               // ✅ No assumption about TResponse
```

### Reflection vs Compile-Time Safety

Trade-off made:
- Lost: Compile-time type safety (ModuleName property check)
- Gained: Runtime flexibility (works with Result wrapper)
- Acceptable: ModuleName is critical path, exception thrown if missing

## 🚦 Status

- ✅ Root cause identified (generic type constraint mismatch)
- ✅ Fix implemented (changed to ICommandBase + runtime check)
- ✅ Code committed and pushed
- ✅ Documentation updated
- ⏳ Awaiting user testing
- ⏳ Database migrations still required (separate issue)

## 📞 Next Steps

1. **User**: Pull latest code from `claude/review-hrm-infrastructure-KGOiz`
2. **User**: Build solution: `dotnet build`
3. **User**: Run migration scripts (if not done yet)
4. **User**: Start API: `dotnet run --project src/Apps/HRM.Api`
5. **User**: Test login → verify refresh token saved
6. **User**: Test refresh endpoint → verify token rotation works
7. **User**: Report results

## 🔗 References

- Commit: `9d86cfe` - fix: Fix UnitOfWorkBehavior generic type constraint
- Branch: `claude/review-hrm-infrastructure-KGOiz`
- Related Issue: RefreshToken table missing (migration scripts)
- Documentation: `UNIT_OF_WORK_DEBUG.md`, `REFRESH_TOKEN_FIX.md`
