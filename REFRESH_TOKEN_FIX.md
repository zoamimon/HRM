# Refresh Token Issue - Diagnostic and Fix

## Problem Summary

**Symptom**: Login operator admin thành công nhưng refresh token KHÔNG được lưu vào database.

**Root Cause**: Database migration scripts cho RefreshTokens table chưa được chạy.

## Detailed Analysis

### What's Working ✅

1. **Code đã được commit đầy đủ**:
   - ✅ `LoginCommand` và `LoginCommandHandler`
   - ✅ `RefreshToken` entity và repository
   - ✅ `UnitOfWorkBehavior` để commit changes
   - ✅ SQL migration scripts (004, 005)

2. **Login flow hoạt động**:
   - ✅ Username/password validation
   - ✅ JWT access token generation
   - ✅ Refresh token generation
   - ✅ Response trả về với access token và refresh token

### What's NOT Working ❌

**RefreshToken không được persist vào database** vì:

1. ❌ Bảng `Identity.RefreshTokens` CHƯA tồn tại trong database
2. ❌ Migration scripts 004 và 005 CHƯA được chạy

### Code Flow Analysis

```csharp
// LoginCommandHandler.cs (Line 131-141)
var refreshTokenEntity = Domain.Entities.RefreshToken.Create(
    UserType.Operator,
    @operator.Id,
    refreshToken,
    refreshTokenExpiry,
    request.IpAddress,
    request.UserAgent
);

_refreshTokenRepository.Add(refreshTokenEntity);  // ← Add to EF context
// UnitOfWorkBehavior will commit  ← Expects CommitAsync to save

// UnitOfWorkBehavior.cs (Line 80)
await unitOfWork.CommitAsync(cancellationToken);  // ← Calls SaveChangesAsync

// At this point EF Core tries:
// INSERT INTO [Identity].[RefreshTokens] (...)
// VALUES (...)

// ❌ FAIL: Table 'HrmDb.Identity.RefreshTokens' doesn't exist
```

### What Happens When Table Doesn't Exist

**Scenario 1: Exception is thrown**
```
Microsoft.Data.SqlClient.SqlException:
Invalid object name 'Identity.RefreshTokens'.
```
- Login endpoint returns 500 Internal Server Error
- Or exception is caught and logged

**Scenario 2: Exception is swallowed**
- Login appears successful (returns 200 OK)
- Access token works
- Refresh token returned to client
- But refresh token is NOT in database
- When client tries to refresh → fails with "Invalid refresh token"

## The Fix

### Required Migration Scripts

Two SQL scripts need to be executed:

1. **004_CreateRefreshTokensTable.sql**
   - Creates `Identity.RefreshTokens` table
   - Adds columns: Id, OperatorId, Token, ExpiresAt, etc.
   - Creates indexes for performance

2. **005_MigrateRefreshTokensToPolymorphic.sql**
   - Adds `UserType` column (support multiple user types)
   - Renames `OperatorId` → `PrincipalId`
   - Updates indexes for polymorphic queries

### Solution Options

#### Option 1: Automated Script (Recommended) ⭐

```bash
cd src/Database/Identity
./run-all-migrations.sh localhost HrmDb sa YourStrong@Passw0rd
```

This script:
- ✅ Tests database connection
- ✅ Creates database if not exists
- ✅ Executes all 5 migration scripts in order
- ✅ Shows summary (success/skip/fail counts)
- ✅ Provides verification commands

#### Option 2: Manual Execution (SSMS)

1. Open SQL Server Management Studio
2. Connect to your SQL Server instance
3. Open and execute in order:
   - `001_CreateOperatorsTable.sql` (if not run yet)
   - `002_CreateIndexes.sql` (if not run yet)
   - `003_SeedAdminOperator.sql` (if not run yet)
   - **`004_CreateRefreshTokensTable.sql`** ← **REQUIRED**
   - **`005_MigrateRefreshTokensToPolymorphic.sql`** ← **REQUIRED**

#### Option 3: sqlcmd (Command Line)

```bash
# Navigate to scripts directory
cd src/Database/Identity

# Run migrations (replace with your credentials)
sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -d HrmDb -i 004_CreateRefreshTokensTable.sql
sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -d HrmDb -i 005_MigrateRefreshTokensToPolymorphic.sql
```

## Verification

After running migrations, verify the fix:

### 1. Check Table Exists

```sql
SELECT * FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'Identity' AND TABLE_NAME = 'RefreshTokens'

-- Expected: 1 row returned
```

### 2. Check Table Schema

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'Identity' AND TABLE_NAME = 'RefreshTokens'
ORDER BY ORDINAL_POSITION

-- Expected columns:
-- Id, PrincipalId, UserType, Token, ExpiresAt, RevokedAt, etc.
```

### 3. Check Polymorphic Design

```sql
-- Should have UserType column (not OperatorId)
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'Identity'
  AND TABLE_NAME = 'RefreshTokens'
  AND COLUMN_NAME = 'UserType'

-- Expected: 1 row (UserType exists)

-- Should NOT have OperatorId (renamed to PrincipalId)
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'Identity'
  AND TABLE_NAME = 'RefreshTokens'
  AND COLUMN_NAME = 'OperatorId'

-- Expected: 0 rows (OperatorId should not exist)
```

### 4. Test Login Flow

```bash
# Start your application
cd src/Apps/HRM.Api
dotnet run

# Test login endpoint
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
#   "refreshToken": "8f3d7b2a9e1c...",
#   "accessTokenExpiry": "2024-01-15T10:30:00Z",
#   "refreshTokenExpiry": "2024-01-22T10:15:00Z",
#   "user": { ... }
# }
```

### 5. Verify Token Saved in Database

```sql
-- After successful login, check if token was saved
SELECT TOP 5
    Id,
    UserType,
    PrincipalId,
    Token,
    ExpiresAt,
    CreatedAtUtc,
    CreatedByIp,
    UserAgent
FROM Identity.RefreshTokens
ORDER BY CreatedAtUtc DESC

-- Expected: 1 or more rows (your recent login sessions)
```

## Common Issues

### Issue 1: "Invalid object name 'Identity.RefreshTokens'"

**Cause**: Script 004 not executed

**Fix**: Run `004_CreateRefreshTokensTable.sql`

### Issue 2: "Invalid column name 'UserType'"

**Cause**: Script 005 not executed

**Fix**: Run `005_MigrateRefreshTokensToPolymorphic.sql`

### Issue 3: "Invalid column name 'OperatorId'"

**Cause**: Script 005 was executed (renamed OperatorId → PrincipalId) but code still uses old name

**Fix**: This should NOT happen - code already uses PrincipalId. Check your code version.

### Issue 4: Login succeeds but token not in database

**Causes**:
1. Migration scripts not run → Run 004 and 005
2. UnitOfWorkBehavior not configured → Check DI registration
3. Exception thrown but swallowed → Check logs

**Debug**:
```bash
# Enable EF Core logging in appsettings.json
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}

# Check logs for SQL INSERT statement
# Should see: INSERT INTO [Identity].[RefreshTokens] ...
```

## Summary

**Problem**: RefreshToken table doesn't exist → tokens not saved

**Solution**: Run migration scripts 004 and 005

**Quick Fix**:
```bash
cd src/Database/Identity
./run-all-migrations.sh
```

**Verify**:
```sql
SELECT COUNT(*) FROM Identity.RefreshTokens
-- After login should be > 0
```

## Next Steps

After fixing:

1. ✅ Run migration scripts
2. ✅ Verify tables exist
3. ✅ Test login flow
4. ✅ Verify token saved in database
5. ✅ Test refresh token endpoint
6. ✅ Test logout (token revocation)
7. ✅ Test session management

## References

- Migration Scripts: `src/Database/Identity/004_*.sql`, `005_*.sql`
- Code: `LoginCommandHandler.cs`, `RefreshTokenRepository.cs`
- Documentation: `src/Database/Identity/README.md`
