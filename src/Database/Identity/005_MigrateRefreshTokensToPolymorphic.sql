-- =============================================
-- Script: Migrate RefreshTokens to Polymorphic Design
-- Module: Identity
-- Purpose: Convert RefreshTokens table from Operator-only to support multiple user types
-- Dependencies: 004_CreateRefreshTokensTable.sql
-- =============================================

PRINT 'Starting RefreshTokens polymorphic migration...'
GO

-- =============================================
-- Phase 1: Add UserType column with default value
-- =============================================

PRINT '  [1/7] Adding UserType column...'
GO

-- Add UserType column (default to 1 = Operator for existing data)
ALTER TABLE [Identity].RefreshTokens
ADD UserType TINYINT NOT NULL DEFAULT 1
GO

-- Add CHECK constraint for valid UserType values
ALTER TABLE [Identity].RefreshTokens
ADD CONSTRAINT CHK_RefreshTokens_UserType
    CHECK (UserType IN (1, 2))  -- 1=Operator, 2=Employee
GO

PRINT '  ✓ UserType column added'
GO

-- =============================================
-- Phase 2: Rename OperatorId to PrincipalId
-- =============================================

PRINT '  [2/7] Renaming OperatorId to PrincipalId...'
GO

EXEC sp_rename
    'Identity.RefreshTokens.OperatorId',
    'PrincipalId',
    'COLUMN'
GO

PRINT '  ✓ Column renamed'
GO

-- =============================================
-- Phase 3: Drop old Foreign Key constraint
-- =============================================

PRINT '  [3/7] Dropping old FK constraint...'
GO

-- Drop FK constraint FK_RefreshTokens_Operators_OperatorId
IF EXISTS (
    SELECT * FROM sys.foreign_keys
    WHERE name = 'FK_RefreshTokens_Operators_OperatorId'
    AND parent_object_id = OBJECT_ID('[Identity].RefreshTokens')
)
BEGIN
    ALTER TABLE [Identity].RefreshTokens
    DROP CONSTRAINT FK_RefreshTokens_Operators_OperatorId
    PRINT '  ✓ Old FK constraint dropped'
END
ELSE
BEGIN
    PRINT '  ⚠ FK constraint not found (already dropped or using different name)'
END
GO

-- =============================================
-- Phase 4: Drop old indexes
-- =============================================

PRINT '  [4/7] Dropping old indexes...'
GO

-- Drop old index IX_RefreshTokens_OperatorId
IF EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = 'IX_RefreshTokens_OperatorId'
    AND object_id = OBJECT_ID('[Identity].RefreshTokens')
)
BEGIN
    DROP INDEX IX_RefreshTokens_OperatorId ON [Identity].RefreshTokens
    PRINT '  ✓ Index IX_RefreshTokens_OperatorId dropped'
END
ELSE
BEGIN
    PRINT '  ⚠ Index IX_RefreshTokens_OperatorId not found'
END
GO

-- Drop old index IX_RefreshTokens_OperatorId_Active if exists
IF EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = 'IX_RefreshTokens_OperatorId_Active'
    AND object_id = OBJECT_ID('[Identity].RefreshTokens')
)
BEGIN
    DROP INDEX IX_RefreshTokens_OperatorId_Active ON [Identity].RefreshTokens
    PRINT '  ✓ Index IX_RefreshTokens_OperatorId_Active dropped'
END
ELSE
BEGIN
    PRINT '  ⚠ Index IX_RefreshTokens_OperatorId_Active not found'
END
GO

-- =============================================
-- Phase 5: Create new polymorphic indexes
-- =============================================

PRINT '  [5/7] Creating new polymorphic indexes...'
GO

-- Composite index for active sessions query
-- Optimized for: WHERE UserType = @type AND PrincipalId = @id AND RevokedAt IS NULL AND ExpiresAt > NOW
CREATE NONCLUSTERED INDEX IX_RefreshTokens_Principal_Active
    ON [Identity].RefreshTokens (UserType, PrincipalId, ExpiresAt)
    INCLUDE (Token, CreatedAtUtc, UserAgent, CreatedByIp)
    WHERE RevokedAt IS NULL
GO

PRINT '  ✓ Composite index IX_RefreshTokens_Principal_Active created'
GO

-- =============================================
-- Phase 6: Update extended properties
-- =============================================

PRINT '  [6/7] Updating extended properties...'
GO

-- Update PrincipalId column description
IF EXISTS (
    SELECT * FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('[Identity].RefreshTokens')
    AND minor_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID('[Identity].RefreshTokens') AND name = 'PrincipalId')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_dropextendedproperty
        @name = N'MS_Description',
        @level0type = N'SCHEMA', @level0name = N'Identity',
        @level1type = N'TABLE', @level1name = N'RefreshTokens',
        @level2type = N'COLUMN', @level2name = N'PrincipalId'
END
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Polymorphic foreign key - ID of user (Operator, Employee, etc.) who owns this token. References different tables based on UserType: 1=Operators.Id, 2=Employees.Id',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'PrincipalId'
GO

-- Add UserType column description
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Type of user who owns this token: 1=Operator (internal users), 2=Employee (company staff). Used as discriminator for polymorphic association.',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'UserType'
GO

PRINT '  ✓ Extended properties updated'
GO

-- =============================================
-- Phase 7: Validation
-- =============================================

PRINT '  [7/7] Validating migration...'
GO

-- Verify table structure
IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('[Identity].RefreshTokens')
    AND name = 'UserType'
)
BEGIN
    RAISERROR('ERROR: UserType column not found!', 16, 1)
    RETURN
END

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('[Identity].RefreshTokens')
    AND name = 'PrincipalId'
)
BEGIN
    RAISERROR('ERROR: PrincipalId column not found!', 16, 1)
    RETURN
END

IF EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('[Identity].RefreshTokens')
    AND name = 'OperatorId'
)
BEGIN
    RAISERROR('ERROR: OperatorId column still exists!', 16, 1)
    RETURN
END

-- Verify new index exists
IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = 'IX_RefreshTokens_Principal_Active'
    AND object_id = OBJECT_ID('[Identity].RefreshTokens')
)
BEGIN
    RAISERROR('ERROR: New polymorphic index not found!', 16, 1)
    RETURN
END

-- Verify CHECK constraint exists
IF NOT EXISTS (
    SELECT * FROM sys.check_constraints
    WHERE name = 'CHK_RefreshTokens_UserType'
    AND parent_object_id = OBJECT_ID('[Identity].RefreshTokens')
)
BEGIN
    RAISERROR('ERROR: UserType CHECK constraint not found!', 16, 1)
    RETURN
END

PRINT '  ✓ Migration validation passed'
GO

-- =============================================
-- Summary
-- =============================================

PRINT ''
PRINT '✅ RefreshTokens polymorphic migration completed successfully!'
PRINT ''
PRINT 'Summary:'
PRINT '  - UserType column added (TINYINT, default 1=Operator)'
PRINT '  - OperatorId renamed to PrincipalId'
PRINT '  - Old FK constraint dropped (polymorphic design cannot have DB FK)'
PRINT '  - Old indexes dropped'
PRINT '  - New composite index created: IX_RefreshTokens_Principal_Active'
PRINT '  - CHECK constraint added for valid UserType values'
PRINT '  - Extended properties updated'
PRINT ''
PRINT '⚠️  IMPORTANT: Application-level validation required!'
PRINT '  - Verify PrincipalId exists before creating token'
PRINT '  - Use Domain Service to ensure referential integrity'
PRINT '  - Database cannot enforce FK constraint (polymorphic limitation)'
PRINT ''
PRINT 'Next steps:'
PRINT '  1. Update application code to use UserType + PrincipalId'
PRINT '  2. Test with both Operator and Employee users'
PRINT '  3. Monitor query performance with new indexes'
PRINT ''
GO
