-- =============================================
-- Script: Create RolePermissions Table
-- Module: Identity
-- Purpose: Store permissions assigned to roles (owned entity pattern)
-- Dependencies: 006_CreateRolesTable.sql
-- =============================================

USE HrmDb
GO

-- =============================================
-- Create RolePermissions Table
-- =============================================
-- Design: Owned Entity pattern
-- - Each RolePermission is owned by a Role
-- - Cascade delete when Role is deleted
-- - Value object stored as table for EF Core compatibility
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RolePermissions' AND schema_id = SCHEMA_ID('Identity'))
BEGIN
    CREATE TABLE Identity.RolePermissions
    (
        -- Composite Primary Key
        Id                  INT                 IDENTITY(1,1) NOT NULL,

        -- Foreign Key to Role
        RoleId              UNIQUEIDENTIFIER    NOT NULL,

        -- Permission Components (from RolePermission value object)
        Module              NVARCHAR(50)        NOT NULL,   -- e.g., "Personnel", "Identity"
        Entity              NVARCHAR(50)        NOT NULL,   -- e.g., "Employee", "Role"
        Action              NVARCHAR(50)        NOT NULL,   -- e.g., "View", "Create"
        Scope               INT                 NULL,       -- ScopeLevel enum: 0=Company, 1=Department, 2=Position, 3=Employee(Self)

        -- Constraints
        CONSTRAINT PK_Identity_RolePermissions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Identity_RolePermissions_Roles FOREIGN KEY (RoleId)
            REFERENCES Identity.Roles (Id)
            ON DELETE CASCADE,  -- Owned entity: delete permissions when role deleted
        CONSTRAINT UQ_Identity_RolePermissions_Unique UNIQUE (RoleId, Module, Entity, Action, Scope)
    )

    PRINT 'Table Identity.RolePermissions created successfully'
END
ELSE
BEGIN
    PRINT 'Table Identity.RolePermissions already exists'
END
GO

-- =============================================
-- Create Indexes
-- =============================================

-- Index: Fast lookup by RoleId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Identity_RolePermissions_RoleId' AND object_id = OBJECT_ID('Identity.RolePermissions'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Identity_RolePermissions_RoleId
    ON Identity.RolePermissions (RoleId)
    INCLUDE (Module, Entity, Action, Scope)

    PRINT 'Index IX_Identity_RolePermissions_RoleId created'
END
GO

-- Index: Permission lookup (for checking if any role has specific permission)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Identity_RolePermissions_Permission' AND object_id = OBJECT_ID('Identity.RolePermissions'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Identity_RolePermissions_Permission
    ON Identity.RolePermissions (Module, Entity, Action)
    INCLUDE (RoleId, Scope)

    PRINT 'Index IX_Identity_RolePermissions_Permission created'
END
GO

-- =============================================
-- ScopeLevel Enum Reference
-- =============================================
-- 0 = Company    (Toàn công ty)
-- 1 = Department (Cùng phòng ban)
-- 2 = Position   (Cùng chức danh)
-- 3 = Employee   (Chỉ bản thân / Self)
-- NULL = No scope restriction (for operator roles)
-- =============================================

PRINT 'Script 007_CreateRolePermissionsTable.sql completed'
GO
