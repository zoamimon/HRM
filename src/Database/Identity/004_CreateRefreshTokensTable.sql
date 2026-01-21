-- =============================================
-- Script: Create RefreshTokens Table
-- Module: Identity
-- Purpose: Create RefreshTokens table for JWT session management
-- Dependencies: 001_CreateOperatorsTable.sql (Operators table must exist)
-- =============================================

-- Drop table if exists (for development only - remove in production)
IF OBJECT_ID('Identity.RefreshTokens', 'U') IS NOT NULL
BEGIN
    DROP TABLE Identity.RefreshTokens
    PRINT 'Table [Identity].[RefreshTokens] dropped'
END
GO

-- Create RefreshTokens table
CREATE TABLE Identity.RefreshTokens
(
    -- Primary Key
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),

    -- Foreign Key to Operators
    OperatorId UNIQUEIDENTIFIER NOT NULL,

    -- Token Information
    Token NVARCHAR(200) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,

    -- Revocation Tracking
    RevokedAt DATETIME2 NULL,
    RevokedByIp NVARCHAR(50) NULL,
    ReplacedByToken NVARCHAR(200) NULL,

    -- Session Tracking
    CreatedByIp NVARCHAR(50) NOT NULL,
    UserAgent NVARCHAR(500) NULL,

    -- Audit Trail (from Entity base class)
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedAtUtc DATETIME2 NULL,
    CreatedById UNIQUEIDENTIFIER NULL,
    ModifiedById UNIQUEIDENTIFIER NULL,

    -- Soft Delete (from ISoftDeletable interface)
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAtUtc DATETIME2 NULL,

    -- Primary Key Constraint
    CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),

    -- Foreign Key Constraint
    CONSTRAINT FK_RefreshTokens_Operators_OperatorId
        FOREIGN KEY (OperatorId)
        REFERENCES Identity.Operators(Id)
        ON DELETE CASCADE, -- Delete tokens when operator deleted

    -- Unique Constraint (token must be unique)
    CONSTRAINT UQ_RefreshTokens_Token UNIQUE (Token)
)
GO

-- Create Indexes for Performance

-- 1. Index on Token for fast lookup during validation
-- Already covered by UNIQUE constraint (creates unique index automatically)

-- 2. Index on OperatorId for loading user's sessions
CREATE NONCLUSTERED INDEX IX_RefreshTokens_OperatorId
    ON Identity.RefreshTokens(OperatorId)
    INCLUDE (Token, ExpiresAt, RevokedAt, CreatedAtUtc, UserAgent, CreatedByIp)
GO

-- 3. Index on ExpiresAt for cleanup job (delete expired tokens)
CREATE NONCLUSTERED INDEX IX_RefreshTokens_ExpiresAt
    ON Identity.RefreshTokens(ExpiresAt)
    WHERE RevokedAt IS NULL -- Only index active tokens
GO

-- 4. Composite index for active sessions query
-- Used by GetActiveSessionsQuery: WHERE OperatorId = @id AND RevokedAt IS NULL AND ExpiresAt > GETUTCDATE()
CREATE NONCLUSTERED INDEX IX_RefreshTokens_OperatorId_Active
    ON Identity.RefreshTokens(OperatorId, RevokedAt, ExpiresAt)
    INCLUDE (Token, CreatedAtUtc, UserAgent, CreatedByIp)
    WHERE RevokedAt IS NULL AND ExpiresAt > GETUTCDATE()
GO

-- Add extended properties (documentation)
EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'RefreshTokens table - stores refresh tokens for JWT authentication and session management',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Unique refresh token identifier (GUID)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'Id'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Foreign key to Operators table (owner of this session)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'OperatorId'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Refresh token value (Base64-encoded random string, ~88 chars for 64 bytes)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'Token'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'When token expires (UTC). Normal: 7 days, Remember Me: 30 days',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'ExpiresAt'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'When token was revoked (UTC). NULL if still active',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'RevokedAt'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'IP address from which token was revoked (audit trail)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'RevokedByIp'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'New token that replaced this one (token rotation chain)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'ReplacedByToken'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'IP address from which token was created (session tracking)',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'CreatedByIp'
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'User agent (browser/device) that created the token. Example: "Mozilla/5.0 (Windows NT 10.0; Win64; x64)..."',
    @level0type = N'SCHEMA', @level0name = N'Identity',
    @level1type = N'TABLE', @level1name = N'RefreshTokens',
    @level2type = N'COLUMN', @level2name = N'UserAgent'
GO

PRINT 'Table [Identity].[RefreshTokens] created successfully with indexes'
GO
