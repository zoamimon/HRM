-- =============================================
-- Script: Create Roles Table
-- Module: Identity
-- Purpose: Store roles for permission-based authorization
-- Dependencies: None
-- =============================================

USE HrmDb
GO

-- =============================================
-- Create Roles Table
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Roles' AND schema_id = SCHEMA_ID('Identity'))
BEGIN
    CREATE TABLE [Identity].Roles
    (
        -- Primary Key
        Id                  UNIQUEIDENTIFIER    NOT NULL,

        -- Role Information
        Name                NVARCHAR(100)       NOT NULL,
        Description         NVARCHAR(500)       NULL,
        IsOperatorRole      BIT                 NOT NULL DEFAULT 0,

        -- Audit Fields (from Entity base class)
        CreatedAtUtc        DATETIME2(7)        NOT NULL,
        ModifiedAtUtc       DATETIME2(7)        NULL,
        CreatedById         UNIQUEIDENTIFIER    NULL,
        ModifiedById        UNIQUEIDENTIFIER    NULL,

        -- Soft Delete
        IsDeleted           BIT                 NOT NULL DEFAULT 0,
        DeletedAtUtc        DATETIME2(7)        NULL,

        -- Constraints
        CONSTRAINT PK_Identity_Roles PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Identity_Roles_Name UNIQUE (Name)
    )

    PRINT 'Table [Identity].Roles created successfully'
END
ELSE
BEGIN
    PRINT 'Table [Identity].Roles already exists'
END
GO

-- =============================================
-- Create Indexes
-- =============================================

-- Index: Filter by IsOperatorRole
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Identity_Roles_IsOperatorRole' AND object_id = OBJECT_ID('[Identity].Roles'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Identity_Roles_IsOperatorRole
    ON [Identity].Roles (IsOperatorRole)
    WHERE IsDeleted = 0

    PRINT 'Index IX_Identity_Roles_IsOperatorRole created'
END
GO

-- Index: Filter by IsDeleted for soft delete queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Identity_Roles_IsDeleted' AND object_id = OBJECT_ID('[Identity].Roles'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Identity_Roles_IsDeleted
    ON [Identity].Roles (IsDeleted)
    INCLUDE (Name, IsOperatorRole)

    PRINT 'Index IX_Identity_Roles_IsDeleted created'
END
GO

PRINT 'Script 006_CreateRolesTable.sql completed'
GO
