-- =============================================
-- Script: Update Scope Level Values
-- Module: Identity
-- Purpose: Update Scope column to match ScopeLevel enum
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
-- NEW Scope Values (matches ScopeLevel enum, lower = wider access):
--   0 = Global (system-wide access, super admin)
--   1 = Company (whole company)
--   2 = Department (same department)
--   3 = Position (same position/team)
--   4 = Employee (only own data, self)
--   NULL = Global (default for backward compatibility)
--
-- =============================================

USE HrmDb
GO

-- =============================================
-- Step 1: Update existing Scope values
-- =============================================
-- Mapping (note: do in correct order to avoid conflicts):
--   OLD 3 (Employee/Self) -> NEW 4 (Employee)
--   OLD 2 (Position) -> NEW 3 (Position)
--   OLD 1 (Department) -> NEW 2 (Department)
--   OLD 0 (Company) -> NEW 1 (Company)
--   NULL -> NEW 0 (Global) for operator permissions
-- =============================================

BEGIN TRANSACTION

BEGIN TRY
    -- Step 1: Update Employee/Self scope first: 3 -> 4
    UPDATE [Identity].RolePermissions
    SET Scope = 4
    WHERE Scope = 3

    PRINT 'Updated Employee/Self scope (3 -> 4)'

    -- Step 2: Update Position scope: 2 -> 3
    UPDATE [Identity].RolePermissions
    SET Scope = 3
    WHERE Scope = 2

    PRINT 'Updated Position scope (2 -> 3)'

    -- Step 3: Update Department scope: 1 -> 2
    UPDATE [Identity].RolePermissions
    SET Scope = 2
    WHERE Scope = 1

    PRINT 'Updated Department scope (1 -> 2)'

    -- Step 4: Update Company scope: 0 -> 1
    UPDATE [Identity].RolePermissions
    SET Scope = 1
    WHERE Scope = 0

    PRINT 'Updated Company scope (0 -> 1)'

    -- Step 5: Update NULL scope to Global (0) for Identity module permissions
    UPDATE [Identity].RolePermissions
    SET Scope = 0
    WHERE Scope IS NULL
      AND Module = 'Identity'

    PRINT 'Updated NULL scope to Global (0) for Identity module'

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
        @value = N'Scope level (ScopeLevel enum): 0=Global, 1=Company, 2=Department, 3=Position, 4=Employee',
        @level0type = N'SCHEMA', @level0name = N'Identity',
        @level1type = N'TABLE', @level1name = N'RolePermissions',
        @level2type = N'COLUMN', @level2name = N'Scope'
END
ELSE
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Scope level (ScopeLevel enum): 0=Global, 1=Company, 2=Department, 3=Position, 4=Employee',
        @level0type = N'SCHEMA', @level0name = N'Identity',
        @level1type = N'TABLE', @level1name = N'RolePermissions',
        @level2type = N'COLUMN', @level2name = N'Scope'
END
GO

PRINT 'Script 010_UpdateScopeLevels.sql completed'
GO
