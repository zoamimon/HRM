-- =============================================
-- Script: Create Operators Table
-- Module: Identity
-- Purpose: Create Operators table with all required columns
-- Dependencies: None (first script to run)
-- =============================================

-- Create Identity schema if not exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Identity')
BEGIN
    EXEC('CREATE SCHEMA [Identity]')
    PRINT 'Schema [Identity] created successfully'
END
ELSE
BEGIN
    PRINT 'Schema [Identity] already exists'
END
GO

-- Drop table if exists (for development only - remove in production)
IF OBJECT_ID('Identity.Operators', 'U') IS NOT NULL
BEGIN
    DROP TABLE Identity.Operators
    PRINT 'Table [Identity].[Operators] dropped'
END
GO

-- Create Operators table
CREATE TABLE Identity.Operators
(
    -- Primary Key
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    -- Identity Information
    Username NVARCHAR(50) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(200) NOT NULL,
    PhoneNumber NVARCHAR(20) NULL,

    -- Status Management
    Status INT NOT NULL DEFAULT 0, -- 0=Pending, 1=Active, 2=Suspended, 3=Deactivated
    ActivatedAtUtc DATETIME2 NULL,
    LastLoginAtUtc DATETIME2 NULL,

    -- Security Features
    IsTwoFactorEnabled BIT NOT NULL DEFAULT 0,
    TwoFactorSecret NVARCHAR(255) NULL,
    FailedLoginAttempts INT NOT NULL DEFAULT 0,
    LockedUntilUtc DATETIME2 NULL,

    -- Audit Trail (from Entity base class)
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedAtUtc DATETIME2 NULL,
    CreatedById UNIQUEIDENTIFIER NULL,
    ModifiedById UNIQUEIDENTIFIER NULL,

    -- Soft Delete (from ISoftDeletable interface)
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAtUtc DATETIME2 NULL,

    -- Primary Key Constraint
    CONSTRAINT PK_Operators PRIMARY KEY CLUSTERED (Id),

    -- Unique Constraints
    CONSTRAINT UQ_Operators_Username UNIQUE (Username),
    CONSTRAINT UQ_Operators_Email UNIQUE (Email),

    -- Check Constraints
    CONSTRAINT CK_Operators_Status CHECK (Status BETWEEN 0 AND 3),
    CONSTRAINT CK_Operators_FailedLoginAttempts CHECK (FailedLoginAttempts >= 0)
)
GO

-- Add extended properties (documentation)
EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Operators table - stores operator accounts for system access',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'Operators'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Unique operator identifier (GUID)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'Operators',
    @level2type = N'COLUMN', @level2name = N'Id'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Username for login (3-50 chars, alphanumeric with underscores/hyphens)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'Operators',
    @level2type = N'COLUMN', @level2name = N'Username'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Email address (unique, used for notifications)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'Operators',
    @level2type = N'COLUMN', @level2name = N'Email'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'BCrypt password hash (never store plaintext passwords)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'Operators',
    @level2type = N'COLUMN', @level2name = N'PasswordHash'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Operator status: 0=Pending, 1=Active, 2=Suspended, 3=Deactivated',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'Operators',
    @level2type = N'COLUMN', @level2name = N'Status'
GO

PRINT 'Table [Identity].[Operators] created successfully'
GO
