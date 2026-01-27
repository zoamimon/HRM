-- =============================================
-- Script: Update Scope Level Values
-- Module: Identity
-- Purpose: Update Scope column to match new hierarchy design
-- Dependencies: 007_CreateRolePermissionsTable.sql
-- =============================================
--
-- OLD Scope Values:
--   0 = Company
--   1 = Department
--   2 = Position
--   3 = Employee (Self)
--   NULL = No scope restriction
--
-- NEW Scope Values (higher = more access):
--   4 = Global (system-wide access, super admin)
--   3 = Company (whole company)
--   2 = Department (same department)
--   1 = Self (only own data)
--   NULL = No scope (for backward compatibility)
--
-- =============================================

USE HrmDb
GO

-- =============================================
-- Step 1: Update existing Scope values
-- =============================================
-- Mapping:
--   OLD 0 (Company) -> NEW 3 (Company)
--   OLD 1 (Department) -> NEW 2 (Department)
--   OLD 2 (Position) -> removed (use Department instead)
--   OLD 3 (Employee/Self) -> NEW 1 (Self)
--   NULL -> NEW 4 (Global) for operator permissions
-- =============================================

BEGIN TRANSACTION

BEGIN TRY
    -- Update Company scope: 0 -> 3
    UPDATE [Identity].RolePermissions
    SET Scope = 3
    WHERE Scope = 0

    PRINT 'Updated Company scope (0 -> 3)'

    -- Update Self scope: 3 -> 1 (do this before Department to avoid conflict)
    UPDATE [Identity].RolePermissions
    SET Scope = 1
    WHERE Scope = 3

    PRINT 'Updated Self scope (3 -> 1)'

    -- Update Department scope: 1 -> 2
    UPDATE [Identity].RolePermissions
    SET Scope = 2
    WHERE Scope = 1

    PRINT 'Updated Department scope (1 -> 2)'

    -- Update Position scope to Department: 2 -> 2 (no change needed, already 2)
    -- Position scope is deprecated, treat as Department

    -- Update NULL scope to Global (4) for Identity module permissions
    UPDATE [Identity].RolePermissions
    SET Scope = 4
    WHERE Scope IS NULL
      AND Module = 'Identity'

    PRINT 'Updated NULL scope to Global (4) for Identity module'

    COMMIT TRANSACTION
    PRINT 'Scope level migration completed successfully'
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION
    PRINT 'Error during scope migration: ' + ERROR_MESSAGE()
    THROW
END CATCH
GO

-- =============================================
-- Step 2: Update table comment (for documentation)
-- =============================================
-- Add extended property to document new scope values
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE name = 'MS_Description'
           AND major_id = OBJECT_ID('[Identity].RolePermissions')
           AND minor_id = (SELECT column_id FROM sys.columns WHERE name = 'Scope' AND object_id = OBJECT_ID('[Identity].RolePermissions')))
BEGIN
    EXEC sp_updateextendedproperty
        @name = N'MS_Description',
        @value = N'Scope level: 4=Global, 3=Company, 2=Department, 1=Self',
        @level0type = N'SCHEMA', @level0name = N'Identity',
        @level1type = N'TABLE', @level1name = N'RolePermissions',
        @level2type = N'COLUMN', @level2name = N'Scope'
END
ELSE
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Scope level: 4=Global, 3=Company, 2=Department, 1=Self',
        @level0type = N'SCHEMA', @level0name = N'Identity',
        @level1type = N'TABLE', @level1name = N'RolePermissions',
        @level2type = N'COLUMN', @level2name = N'Scope'
END
GO

PRINT 'Script 010_UpdateScopeLevels.sql completed'
GO
