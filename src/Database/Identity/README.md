# Identity Module Database Scripts

This directory contains SQL scripts for setting up the Identity module database schema and seed data.

## Overview

The Identity module uses SQL Server with schema separation (`Identity` schema) in a shared database (`HrmDb`).

**NO Entity Framework Migrations** - These scripts should be run manually or via deployment pipeline.

## Prerequisites

- SQL Server 2019 or later (SQL Server 2022 recommended)
- Database: `HrmDb` (created before running scripts)
- SQL Server Authentication or Windows Authentication
- User with `db_owner` or equivalent permissions

## Script Execution Order

**CRITICAL**: Scripts must be executed in the following order:

1. **001_CreateOperatorsTable.sql** - Creates Identity schema and Operators table
2. **002_CreateIndexes.sql** - Creates indexes for performance optimization
3. **003_SeedAdminOperator.sql** - Seeds default admin operator
4. **004_CreateRefreshTokensTable.sql** - Creates RefreshTokens table for JWT session management
5. **005_MigrateRefreshTokensToPolymorphic.sql** - Migrates RefreshTokens to polymorphic design

## Quick Start

### Option 0: Automated Script (Recommended)

```bash
# Linux/macOS
cd src/Database/Identity
./run-all-migrations.sh localhost HrmDb sa YourStrong@Passw0rd

# The script will:
# - Test connection
# - Create database if not exists
# - Execute all 5 migration scripts in order
# - Show summary with success/skip/fail counts
```

### Option 1: SQL Server Management Studio (SSMS)

```sql
-- 1. Connect to SQL Server instance
-- 2. Ensure database HrmDb exists (create if not):
CREATE DATABASE HrmDb
GO

-- 3. Open and execute scripts in order:
--    File > Open > File > Select script > Execute (F5)
--    001, 002, 003, 004, 005
```

### Option 2: sqlcmd (Command Line)

```bash
# Windows
sqlcmd -S localhost -d HrmDb -i 001_CreateOperatorsTable.sql
sqlcmd -S localhost -d HrmDb -i 002_CreateIndexes.sql
sqlcmd -S localhost -d HrmDb -i 003_SeedAdminOperator.sql
sqlcmd -S localhost -d HrmDb -i 004_CreateRefreshTokensTable.sql
sqlcmd -S localhost -d HrmDb -i 005_MigrateRefreshTokensToPolymorphic.sql

# Linux/Mac (with SQL Server Authentication)
sqlcmd -S localhost -U sa -P YourPassword -d HrmDb -i 001_CreateOperatorsTable.sql
sqlcmd -S localhost -U sa -P YourPassword -d HrmDb -i 002_CreateIndexes.sql
sqlcmd -S localhost -U sa -P YourPassword -d HrmDb -i 003_SeedAdminOperator.sql
sqlcmd -S localhost -U sa -P YourPassword -d HrmDb -i 004_CreateRefreshTokensTable.sql
sqlcmd -S localhost -U sa -P YourPassword -d HrmDb -i 005_MigrateRefreshTokensToPolymorphic.sql
```

### Option 3: Azure Data Studio

```sql
-- 1. Connect to SQL Server
-- 2. Open each script
-- 3. Run (F5)
```

### Option 4: Automated Script (PowerShell - Windows)

```powershell
# run-identity-scripts.ps1
$Server = "localhost"
$Database = "HrmDb"
$ScriptsPath = "src/Database/Identity"

# Execute scripts in order (001, 002, 003, 004, 005)
Get-ChildItem "$ScriptsPath\*.sql" | Sort-Object Name | ForEach-Object {
    Write-Host "Executing $_..."
    sqlcmd -S $Server -d $Database -i $_.FullName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to execute $_"
        exit 1
    }
}

Write-Host "All scripts executed successfully"
```

**Note**: Scripts 004 and 005 are required for refresh token functionality. Without them, login will fail to save refresh tokens.

## Script Details

### 001_CreateOperatorsTable.sql

**Purpose**: Creates the Operators table with all required columns

**Tables Created**:
- `Identity.Operators` - Main operators table

**Columns**:
- `Id` (UNIQUEIDENTIFIER, PK) - Operator ID
- `Username` (NVARCHAR(50), UNIQUE) - Login username
- `Email` (NVARCHAR(255), UNIQUE) - Email address
- `PasswordHash` (NVARCHAR(255)) - BCrypt password hash
- `FullName` (NVARCHAR(200)) - Full name
- `PhoneNumber` (NVARCHAR(20)) - Phone number (optional)
- `Status` (INT) - 0=Pending, 1=Active, 2=Suspended, 3=Deactivated
- `ActivatedAtUtc` (DATETIME2) - Activation timestamp
- `LastLoginAtUtc` (DATETIME2) - Last login timestamp
- `IsTwoFactorEnabled` (BIT) - 2FA enabled flag
- `TwoFactorSecret` (NVARCHAR(255)) - TOTP secret
- `FailedLoginAttempts` (INT) - Failed login counter
- `LockedUntilUtc` (DATETIME2) - Account lock expiry
- `CreatedAtUtc` (DATETIME2) - Creation timestamp
- `ModifiedAtUtc` (DATETIME2) - Last modification timestamp
- `CreatedById` (UNIQUEIDENTIFIER) - Creator operator ID
- `ModifiedById` (UNIQUEIDENTIFIER) - Last modifier operator ID
- `IsDeleted` (BIT) - Soft delete flag
- `DeletedAtUtc` (DATETIME2) - Deletion timestamp

**Constraints**:
- `PK_Operators` - Primary key on Id
- `UQ_Operators_Username` - Unique constraint on Username
- `UQ_Operators_Email` - Unique constraint on Email
- `CK_Operators_Status` - Check Status BETWEEN 0 AND 3
- `CK_Operators_FailedLoginAttempts` - Check FailedLoginAttempts >= 0

**Execution Time**: ~100ms

### 002_CreateIndexes.sql

**Purpose**: Creates indexes for performance optimization

**Indexes Created**:

1. **IX_Operators_Username** (Unique, Filtered)
   - Columns: Username
   - Includes: PasswordHash, Status, IsDeleted, FailedLoginAttempts, LockedUntilUtc
   - Filter: WHERE IsDeleted = 0
   - Use Case: Login queries

2. **IX_Operators_Email** (Unique, Filtered)
   - Columns: Email
   - Includes: Id, Username, FullName
   - Filter: WHERE IsDeleted = 0
   - Use Case: Email uniqueness checks

3. **IX_Operators_Status** (Non-unique, Filtered)
   - Columns: Status, CreatedAtUtc DESC
   - Includes: Id, Username, Email, FullName
   - Filter: WHERE IsDeleted = 0
   - Use Case: Admin dashboards (list pending operators)

4. **IX_Operators_CreatedAtUtc** (Non-unique, Filtered)
   - Columns: CreatedAtUtc DESC
   - Includes: Id, Username, Email, FullName, Status
   - Filter: WHERE IsDeleted = 0
   - Use Case: Chronological sorting, pagination

5. **IX_Operators_IsDeleted** (Non-unique)
   - Columns: IsDeleted, DeletedAtUtc DESC
   - Includes: Id, Username, Email, FullName
   - Use Case: Soft delete queries

**Performance Impact**:
- Login query: ~1-5ms (Index Seek)
- Email lookup: ~1-5ms (Index Seek)
- Status filtering: ~5-10ms (Index Seek + Range Scan)
- Pagination: ~10-20ms (Index Scan)

**Execution Time**: ~200ms

### 003_SeedAdminOperator.sql

**Purpose**: Creates first admin operator for system bootstrapping

**Default Credentials**:
- **Username**: `admin`
- **Password**: `Admin@123456`
- **Email**: `admin@hrm.local`
- **Status**: Active (can login immediately)

**SECURITY WARNINGS**:
1. ⚠️ Change default password immediately after first login
2. ⚠️ Enable two-factor authentication (2FA) for admin account
3. ⚠️ Use strong password (20+ characters with complexity)
4. ⚠️ Rotate password every 90 days
5. ⚠️ Never share admin credentials
6. ⚠️ Monitor admin account activity in audit logs

**Password Hash**:
- Algorithm: BCrypt
- Cost Factor: 11 (2^11 = 2048 rounds)
- Hash: `$2a$11$...` (placeholder - generate real hash using application)

**How to Generate Real Password Hash**:

```csharp
// C# with BCrypt.Net-Next
using BCrypt.Net;

string password = "Admin@123456";
string passwordHash = BCrypt.HashPassword(password, 11);
Console.WriteLine(passwordHash);

// Example output:
// $2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy
```

**Replace Placeholder Hash**:
1. Run above code to generate real BCrypt hash
2. Replace placeholder hash in script with real hash
3. Execute script

**Execution Time**: ~100ms

### 004_CreateRefreshTokensTable.sql

**Purpose**: Creates the RefreshTokens table for JWT session management

**Tables Created**:
- `Identity.RefreshTokens` - Refresh tokens for authentication sessions

**Columns**:
- `Id` (UNIQUEIDENTIFIER, PK) - Token ID
- `OperatorId` (UNIQUEIDENTIFIER, FK → Operators.Id) - Owner of token
- `Token` (NVARCHAR(200), UNIQUE) - Refresh token value (Base64)
- `ExpiresAt` (DATETIME2) - Token expiration timestamp
- `RevokedAt` (DATETIME2, NULL) - Revocation timestamp
- `RevokedByIp` (NVARCHAR(50), NULL) - IP that revoked token
- `ReplacedByToken` (NVARCHAR(200), NULL) - New token in rotation chain
- `CreatedByIp` (NVARCHAR(50)) - IP that created token
- `UserAgent` (NVARCHAR(500), NULL) - Browser/device info
- Audit columns: `CreatedAtUtc`, `ModifiedAtUtc`, etc.
- Soft delete: `IsDeleted`, `DeletedAtUtc`

**Indexes**:
1. `IX_RefreshTokens_OperatorId` - Fast lookup by operator
2. `IX_RefreshTokens_ExpiresAt` - Cleanup expired tokens
3. `IX_RefreshTokens_OperatorId_Active` - Active sessions query

**Constraints**:
- `PK_RefreshTokens` - Primary key
- `FK_RefreshTokens_Operators_OperatorId` - Foreign key to Operators
- `UQ_RefreshTokens_Token` - Unique token value

**Execution Time**: ~150ms

### 005_MigrateRefreshTokensToPolymorphic.sql

**Purpose**: Migrates RefreshTokens table from Operator-only to polymorphic design supporting multiple user types (Operator, Employee, etc.)

**Changes**:
1. Adds `UserType` column (TINYINT, 1=Operator, 2=Employee)
2. Renames `OperatorId` → `PrincipalId` (polymorphic foreign key)
3. Drops old FK constraint (polymorphic design cannot have DB FK)
4. Drops old indexes
5. Creates new composite index `IX_RefreshTokens_Principal_Active`
6. Adds CHECK constraint for valid UserType values

**Migration Phases**:
- Phase 1: Add UserType column (default 1=Operator)
- Phase 2: Rename OperatorId to PrincipalId
- Phase 3: Drop old FK constraint
- Phase 4: Drop old indexes
- Phase 5: Create new polymorphic indexes
- Phase 6: Update extended properties
- Phase 7: Validation

**Execution Time**: ~300ms

**Important**: This migration is safe for existing data. All existing tokens will have `UserType=1` (Operator).

## Verification

After running all scripts, verify the setup:

```sql
-- Check schema exists
SELECT * FROM sys.schemas WHERE name = 'Identity'

-- Check table exists
SELECT * FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'Identity' AND TABLE_NAME = 'Operators'

-- Check indexes
SELECT
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_unique AS IsUnique
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('Identity.Operators')
ORDER BY i.name

-- Check admin operator exists
SELECT
    Id,
    Username,
    Email,
    FullName,
    Status,
    ActivatedAtUtc,
    IsTwoFactorEnabled,
    CreatedAtUtc
FROM Identity.Operators
WHERE Username = 'admin'

-- Expected output:
-- Username: admin
-- Status: 1 (Active)
-- ActivatedAtUtc: (current timestamp)

-- Check RefreshTokens table exists
SELECT * FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'Identity' AND TABLE_NAME = 'RefreshTokens'

-- Check RefreshTokens columns (should have UserType and PrincipalId)
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'Identity' AND TABLE_NAME = 'RefreshTokens'
ORDER BY ORDINAL_POSITION

-- Verify polymorphic index exists
SELECT
    i.name AS IndexName,
    i.type_desc AS IndexType
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('Identity.RefreshTokens')
  AND i.name = 'IX_RefreshTokens_Principal_Active'
```

## Rollback (Development Only)

To drop all Identity module objects:

```sql
-- Drop table (cascades indexes and constraints)
DROP TABLE IF EXISTS Identity.Operators
GO

-- Drop schema
DROP SCHEMA IF EXISTS Identity
GO
```

**⚠️ WARNING**: This will delete all operator data. Use only in development.

## Production Deployment

### Best Practices

1. **Review Scripts**: Inspect all scripts before execution
2. **Backup Database**: Create full backup before running scripts
3. **Test in Staging**: Run scripts in staging environment first
4. **Change Default Password**: Generate unique admin password for production
5. **Secure Credentials**: Store admin credentials in secure vault (Azure Key Vault, AWS Secrets Manager)
6. **Enable 2FA**: Enable two-factor authentication for all admin accounts
7. **Monitor Execution**: Log script execution and verify success
8. **Document Changes**: Record script execution date, version, and executor

### Deployment Pipeline (CI/CD)

```yaml
# Example: Azure DevOps Pipeline
- task: SqlAzureDacpacDeployment@1
  inputs:
    azureSubscription: 'Azure-Subscription'
    serverName: 'hrm-sql-server.database.windows.net'
    databaseName: 'HrmDb'
    sqlUsername: '$(SqlUsername)'
    sqlPassword: '$(SqlPassword)'
    deployType: 'SqlTask'
    sqlFile: 'src/Database/Identity/001_CreateOperatorsTable.sql'

- task: SqlAzureDacpacDeployment@1
  inputs:
    sqlFile: 'src/Database/Identity/002_CreateIndexes.sql'

- task: SqlAzureDacpacDeployment@1
  inputs:
    sqlFile: 'src/Database/Identity/003_SeedAdminOperator.sql'
```

## Troubleshooting

### Error: Database 'HrmDb' does not exist

**Solution**: Create database first:
```sql
CREATE DATABASE HrmDb
GO
```

### Error: Permission denied

**Solution**: Ensure user has `db_owner` or equivalent permissions:
```sql
USE HrmDb
GO
ALTER ROLE db_owner ADD MEMBER [YourUsername]
GO
```

### Error: Schema 'Identity' already exists

**Solution**: Normal if running 001 script again. Script checks and skips if exists.

### Error: Admin operator already exists

**Solution**: Normal if running 003 script again. Script checks and skips if exists.

### Error: Invalid object name 'Identity.Operators'

**Solution**: Run 001 script first to create table.

## Connection String

Add to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "HrmDb": "Server=localhost;Database=HrmDb;User Id=sa;Password=YourPassword;TrustServerCertificate=True;MultipleActiveResultSets=True"
  }
}
```

**Production**: Use Azure SQL Database or SQL Server Always Encrypted for sensitive data.

## Support

For issues or questions:
- Review script comments for detailed documentation
- Check SQL Server error logs: `C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\Log\ERRORLOG`
- Verify prerequisites (SQL Server version, permissions, database exists)
- Contact DBA or DevOps team for production issues

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-01-15 | Initial release |

## License

Internal use only - HRM Project
