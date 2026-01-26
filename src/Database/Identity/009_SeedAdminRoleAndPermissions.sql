-- =============================================
-- Script: Seed Admin Role and Permissions
-- Module: Identity
-- Purpose: Create System Administrator role with full permissions
-- Dependencies: 003_SeedAdminOperator.sql, 006-008 tables
-- =============================================

USE HrmDb
GO

-- =============================================
-- Configuration
-- =============================================
DECLARE @AdminRoleName NVARCHAR(100) = 'System Administrator'
DECLARE @AdminRoleDescription NVARCHAR(500) = 'Full system access. Reserved for system administrators only. Has all permissions across all modules.'

-- =============================================
-- Step 1: Create System Administrator Role
-- =============================================
DECLARE @AdminRoleId UNIQUEIDENTIFIER

-- Check if role already exists
SELECT @AdminRoleId = Id FROM Identity.Roles WHERE Name = @AdminRoleName AND IsDeleted = 0

IF @AdminRoleId IS NULL
BEGIN
    SET @AdminRoleId = NEWID()

    INSERT INTO Identity.Roles
    (
        Id,
        Name,
        Description,
        IsOperatorRole,
        CreatedAtUtc,
        ModifiedAtUtc,
        CreatedById,
        ModifiedById,
        IsDeleted,
        DeletedAtUtc
    )
    VALUES
    (
        @AdminRoleId,
        @AdminRoleName,
        @AdminRoleDescription,
        1,                  -- IsOperatorRole = true (no scope restrictions)
        GETUTCDATE(),
        NULL,
        NULL,               -- System seed (no creator)
        NULL,
        0,
        NULL
    )

    PRINT 'System Administrator role created with ID: ' + CAST(@AdminRoleId AS NVARCHAR(50))
END
ELSE
BEGIN
    PRINT 'System Administrator role already exists with ID: ' + CAST(@AdminRoleId AS NVARCHAR(50))
END
GO

-- =============================================
-- Step 2: Seed All Permissions for Admin Role
-- =============================================
-- Permission format: Module.Entity.Action
-- Scope: NULL for operator roles (no scope restriction)
-- =============================================

DECLARE @AdminRoleId UNIQUEIDENTIFIER
SELECT @AdminRoleId = Id FROM Identity.Roles WHERE Name = 'System Administrator' AND IsDeleted = 0

IF @AdminRoleId IS NOT NULL
BEGIN
    -- Clear existing permissions (for re-seeding)
    DELETE FROM Identity.RolePermissions WHERE RoleId = @AdminRoleId

    -- =============================================
    -- Identity Module Permissions
    -- =============================================
    -- User Entity
    INSERT INTO Identity.RolePermissions (RoleId, Module, Entity, Action, Scope) VALUES
        (@AdminRoleId, 'Identity', 'User', 'View', NULL),
        (@AdminRoleId, 'Identity', 'User', 'Create', NULL),
        (@AdminRoleId, 'Identity', 'User', 'Update', NULL),
        (@AdminRoleId, 'Identity', 'User', 'Delete', NULL),
        (@AdminRoleId, 'Identity', 'User', 'ResetPassword', NULL),
        (@AdminRoleId, 'Identity', 'User', 'AssignPermission', NULL)

    -- Operator Entity
    INSERT INTO Identity.RolePermissions (RoleId, Module, Entity, Action, Scope) VALUES
        (@AdminRoleId, 'Identity', 'Operator', 'View', NULL),
        (@AdminRoleId, 'Identity', 'Operator', 'Create', NULL),
        (@AdminRoleId, 'Identity', 'Operator', 'Update', NULL),
        (@AdminRoleId, 'Identity', 'Operator', 'Delete', NULL),
        (@AdminRoleId, 'Identity', 'Operator', 'ResetPassword', NULL),
        (@AdminRoleId, 'Identity', 'Operator', 'AssignPermission', NULL)

    -- Role Entity
    INSERT INTO Identity.RolePermissions (RoleId, Module, Entity, Action, Scope) VALUES
        (@AdminRoleId, 'Identity', 'Role', 'View', NULL),
        (@AdminRoleId, 'Identity', 'Role', 'Create', NULL),
        (@AdminRoleId, 'Identity', 'Role', 'Update', NULL),
        (@AdminRoleId, 'Identity', 'Role', 'Delete', NULL),
        (@AdminRoleId, 'Identity', 'Role', 'AssignPermission', NULL)

    PRINT 'Identity module permissions seeded: 17 permissions'

    -- =============================================
    -- Personnel Module Permissions (Future)
    -- =============================================
    -- Uncomment when Personnel module is implemented
    /*
    -- Employee Entity
    INSERT INTO Identity.RolePermissions (RoleId, Module, Entity, Action, Scope) VALUES
        (@AdminRoleId, 'Personnel', 'Employee', 'View', NULL),
        (@AdminRoleId, 'Personnel', 'Employee', 'Create', NULL),
        (@AdminRoleId, 'Personnel', 'Employee', 'Update', NULL),
        (@AdminRoleId, 'Personnel', 'Employee', 'Delete', NULL),
        (@AdminRoleId, 'Personnel', 'Employee', 'Export', NULL)

    -- Contract Entity
    INSERT INTO Identity.RolePermissions (RoleId, Module, Entity, Action, Scope) VALUES
        (@AdminRoleId, 'Personnel', 'Contract', 'View', NULL),
        (@AdminRoleId, 'Personnel', 'Contract', 'Create', NULL),
        (@AdminRoleId, 'Personnel', 'Contract', 'Update', NULL),
        (@AdminRoleId, 'Personnel', 'Contract', 'Delete', NULL),
        (@AdminRoleId, 'Personnel', 'Contract', 'Approve', NULL)

    PRINT 'Personnel module permissions seeded'
    */

    -- =============================================
    -- Attendance Module Permissions (Future)
    -- =============================================
    /*
    INSERT INTO Identity.RolePermissions (RoleId, Module, Entity, Action, Scope) VALUES
        (@AdminRoleId, 'Attendance', 'Timesheet', 'View', NULL),
        (@AdminRoleId, 'Attendance', 'Timesheet', 'Create', NULL),
        (@AdminRoleId, 'Attendance', 'Timesheet', 'Update', NULL),
        (@AdminRoleId, 'Attendance', 'Timesheet', 'Delete', NULL),
        (@AdminRoleId, 'Attendance', 'Timesheet', 'Approve', NULL)

    PRINT 'Attendance module permissions seeded'
    */

    -- =============================================
    -- System Module Permissions (Future)
    -- =============================================
    /*
    INSERT INTO Identity.RolePermissions (RoleId, Module, Entity, Action, Scope) VALUES
        (@AdminRoleId, 'System', 'Configuration', 'View', NULL),
        (@AdminRoleId, 'System', 'Configuration', 'Update', NULL),
        (@AdminRoleId, 'System', 'AuditLog', 'View', NULL),
        (@AdminRoleId, 'System', 'AuditLog', 'Export', NULL)

    PRINT 'System module permissions seeded'
    */

    -- Count total permissions
    DECLARE @TotalPermissions INT
    SELECT @TotalPermissions = COUNT(*) FROM Identity.RolePermissions WHERE RoleId = @AdminRoleId
    PRINT 'Total permissions for System Administrator: ' + CAST(@TotalPermissions AS NVARCHAR(10))
END
GO

-- =============================================
-- Step 3: Assign Admin Role to Admin Operator
-- =============================================
DECLARE @AdminOperatorId UNIQUEIDENTIFIER
DECLARE @AdminRoleId UNIQUEIDENTIFIER

SELECT @AdminOperatorId = Id FROM Identity.Operators WHERE Username = 'admin' AND IsDeleted = 0
SELECT @AdminRoleId = Id FROM Identity.Roles WHERE Name = 'System Administrator' AND IsDeleted = 0

IF @AdminOperatorId IS NOT NULL AND @AdminRoleId IS NOT NULL
BEGIN
    -- Check if already assigned
    IF NOT EXISTS (SELECT 1 FROM Identity.OperatorRoles WHERE OperatorId = @AdminOperatorId AND RoleId = @AdminRoleId)
    BEGIN
        INSERT INTO Identity.OperatorRoles (OperatorId, RoleId, AssignedAtUtc, AssignedById)
        VALUES (@AdminOperatorId, @AdminRoleId, GETUTCDATE(), NULL)

        PRINT 'System Administrator role assigned to admin operator'
    END
    ELSE
    BEGIN
        PRINT 'Admin operator already has System Administrator role'
    END
END
ELSE
BEGIN
    IF @AdminOperatorId IS NULL
        PRINT 'WARNING: Admin operator not found. Run 003_SeedAdminOperator.sql first.'
    IF @AdminRoleId IS NULL
        PRINT 'WARNING: System Administrator role not found.'
END
GO

-- =============================================
-- Verification
-- =============================================
PRINT ''
PRINT '================================='
PRINT 'Admin Role and Permissions Summary'
PRINT '================================='

SELECT
    r.Name AS RoleName,
    r.Description,
    r.IsOperatorRole,
    COUNT(rp.Id) AS PermissionCount
FROM Identity.Roles r
LEFT JOIN Identity.RolePermissions rp ON r.Id = rp.RoleId
WHERE r.Name = 'System Administrator' AND r.IsDeleted = 0
GROUP BY r.Id, r.Name, r.Description, r.IsOperatorRole

PRINT ''
PRINT 'Permissions by Module:'
SELECT
    rp.Module,
    COUNT(*) AS PermissionCount
FROM Identity.RolePermissions rp
INNER JOIN Identity.Roles r ON rp.RoleId = r.Id
WHERE r.Name = 'System Administrator' AND r.IsDeleted = 0
GROUP BY rp.Module
ORDER BY rp.Module

PRINT ''
PRINT 'Admin Operator Role Assignment:'
SELECT
    o.Username,
    o.Email,
    r.Name AS RoleName,
    opr.AssignedAtUtc
FROM Identity.Operators o
INNER JOIN Identity.OperatorRoles opr ON o.Id = opr.OperatorId
INNER JOIN Identity.Roles r ON opr.RoleId = r.Id
WHERE o.Username = 'admin' AND o.IsDeleted = 0
GO

PRINT ''
PRINT 'Script 009_SeedAdminRoleAndPermissions.sql completed'
GO
