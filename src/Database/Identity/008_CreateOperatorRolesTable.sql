-- =============================================
-- Script: Create OperatorRoles Table
-- Module: Identity
-- Purpose: Many-to-many relationship between Operators and Roles
-- Dependencies: 001_CreateOperatorsTable.sql, 006_CreateRolesTable.sql
-- =============================================

USE HrmDb
GO

-- =============================================
-- Create OperatorRoles Junction Table
-- =============================================
-- Design: Many-to-many relationship
-- - One Operator can have multiple Roles
-- - One Role can be assigned to multiple Operators
-- - Includes assignment metadata (who assigned, when)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OperatorRoles' AND schema_id = SCHEMA_ID('Identity'))
BEGIN
    CREATE TABLE [Identity].OperatorRoles
    (
        -- Composite Primary Key
        OperatorId          UNIQUEIDENTIFIER    NOT NULL,
        RoleId              UNIQUEIDENTIFIER    NOT NULL,

        -- Assignment Metadata
        AssignedAtUtc       DATETIME2(7)        NOT NULL DEFAULT GETUTCDATE(),
        AssignedById        UNIQUEIDENTIFIER    NULL,       -- Who assigned this role

        -- Constraints
        CONSTRAINT PK_Identity_OperatorRoles PRIMARY KEY CLUSTERED (OperatorId, RoleId),
        CONSTRAINT FK_Identity_OperatorRoles_Operators FOREIGN KEY (OperatorId)
            REFERENCES [Identity].Operators (Id)
            ON DELETE CASCADE,
        CONSTRAINT FK_Identity_OperatorRoles_Roles FOREIGN KEY (RoleId)
            REFERENCES [Identity].Roles (Id)
            ON DELETE CASCADE,
        CONSTRAINT FK_Identity_OperatorRoles_AssignedBy FOREIGN KEY (AssignedById)
            REFERENCES [Identity].Operators (Id)
            ON DELETE NO ACTION
    )

    PRINT 'Table [Identity].OperatorRoles created successfully'
END
ELSE
BEGIN
    PRINT 'Table [Identity].OperatorRoles already exists'
END
GO

-- =============================================
-- Create Indexes
-- =============================================

-- Index: Fast lookup by RoleId (find all operators with a role)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Identity_OperatorRoles_RoleId' AND object_id = OBJECT_ID('[Identity].OperatorRoles'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Identity_OperatorRoles_RoleId
    ON [Identity].OperatorRoles (RoleId)
    INCLUDE (OperatorId, AssignedAtUtc)

    PRINT 'Index IX_Identity_OperatorRoles_RoleId created'
END
GO

-- Index: Fast lookup by AssignedById (audit: who assigned roles)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Identity_OperatorRoles_AssignedById' AND object_id = OBJECT_ID('[Identity].OperatorRoles'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Identity_OperatorRoles_AssignedById
    ON [Identity].OperatorRoles (AssignedById)
    WHERE AssignedById IS NOT NULL

    PRINT 'Index IX_Identity_OperatorRoles_AssignedById created'
END
GO

PRINT 'Script 008_CreateOperatorRolesTable.sql completed'
GO
