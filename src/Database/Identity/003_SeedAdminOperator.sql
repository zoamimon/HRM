-- =============================================
-- Script: Seed Admin Operator
-- Module: Identity
-- Purpose: Create first admin operator for system bootstrapping
-- Dependencies: 001_CreateOperatorsTable.sql, 002_CreateIndexes.sql
-- =============================================

USE HrmDb
GO

-- =============================================
-- Admin Credentials (CHANGE IN PRODUCTION!)
-- =============================================
-- Username: admin
-- Password: Admin@123456
-- BCrypt Hash: $2a$11$... (generated with cost factor 11)
--
-- SECURITY WARNING:
-- - Change default password immediately after first login
-- - Use strong password in production (20+ chars)
-- - Enable 2FA for admin accounts
-- - Rotate passwords regularly (90 days)
-- =============================================

-- Check if admin operator already exists
IF EXISTS (SELECT 1 FROM Identity.Operators WHERE Username = 'admin')
BEGIN
    PRINT 'Admin operator already exists. Skipping seed.'
    RETURN
END
GO

-- BCrypt hash for "Admin@123456" with cost factor 11
-- Generated using BCrypt.Net-Next library
-- IMPORTANT: This is a placeholder hash - generate a real hash using your application
DECLARE @PasswordHash NVARCHAR(255)
SET @PasswordHash = '$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123'

-- Generate new GUID for admin operator
DECLARE @AdminId UNIQUEIDENTIFIER
SET @AdminId = NEWID()

-- Insert admin operator
INSERT INTO Identity.Operators
(
    Id,
    Username,
    Email,
    PasswordHash,
    FullName,
    PhoneNumber,
    Status,
    ActivatedAtUtc,
    LastLoginAtUtc,
    IsTwoFactorEnabled,
    TwoFactorSecret,
    FailedLoginAttempts,
    LockedUntilUtc,
    CreatedAtUtc,
    ModifiedAtUtc,
    CreatedById,
    ModifiedById,
    IsDeleted,
    DeletedAtUtc
)
VALUES
(
    @AdminId,                           -- Id
    'admin',                            -- Username
    'admin@hrm.local',                  -- Email (change to real email in production)
    @PasswordHash,                      -- PasswordHash (BCrypt hash of "Admin@123456")
    'System Administrator',             -- FullName
    NULL,                               -- PhoneNumber
    1,                                  -- Status: Active (can login immediately)
    GETUTCDATE(),                       -- ActivatedAtUtc
    NULL,                               -- LastLoginAtUtc
    0,                                  -- IsTwoFactorEnabled: False (enable in production)
    NULL,                               -- TwoFactorSecret
    0,                                  -- FailedLoginAttempts
    NULL,                               -- LockedUntilUtc
    GETUTCDATE(),                       -- CreatedAtUtc
    NULL,                               -- ModifiedAtUtc
    NULL,                               -- CreatedById (NULL for seed data)
    NULL,                               -- ModifiedById
    0,                                  -- IsDeleted: False
    NULL                                -- DeletedAtUtc
)
GO

-- Verify admin operator was created
IF EXISTS (SELECT 1 FROM Identity.Operators WHERE Username = 'admin')
BEGIN
    PRINT '================================='
    PRINT 'Admin operator created successfully'
    PRINT '================================='
    PRINT ''
    PRINT 'Default Admin Credentials:'
    PRINT '  Username: admin'
    PRINT '  Password: Admin@123456'
    PRINT ''
    PRINT 'SECURITY WARNINGS:'
    PRINT '  1. Change default password immediately after first login'
    PRINT '  2. Enable two-factor authentication (2FA) for admin account'
    PRINT '  3. Use strong password (20+ characters with complexity)'
    PRINT '  4. Rotate password every 90 days'
    PRINT '  5. Never share admin credentials'
    PRINT '  6. Monitor admin account activity in audit logs'
    PRINT ''
    PRINT 'Next Steps:'
    PRINT '  1. Login with admin credentials'
    PRINT '  2. Change password immediately'
    PRINT '  3. Enable 2FA'
    PRINT '  4. Register additional operators as needed'
    PRINT '================================='

    -- Display admin operator details
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
END
ELSE
BEGIN
    PRINT 'ERROR: Failed to create admin operator'
    RAISERROR('Admin operator creation failed', 16, 1)
END
GO

-- =============================================
-- Important Notes for Production:
-- =============================================
-- 1. Password Hash Generation:
--    - DO NOT use the placeholder hash above
--    - Generate real BCrypt hash using your application
--    - C# Example:
--      string passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456", 11);
--
-- 2. Security Best Practices:
--    - Change default password immediately
--    - Enable 2FA for all admin accounts
--    - Use password manager for strong passwords
--    - Implement password rotation policy (90 days)
--    - Monitor failed login attempts
--    - Enable account lockout after 5 failed attempts
--
-- 3. Production Deployment:
--    - Generate unique password for each environment
--    - Store credentials in secure vault (Azure Key Vault, AWS Secrets Manager)
--    - Never commit production credentials to source control
--    - Use different admin credentials for dev/staging/production
--
-- 4. Audit Trail:
--    - All admin actions should be logged
--    - Review admin activity logs regularly
--    - Alert on suspicious activity (login from new IP, unusual hours, etc.)
--    - Implement approval workflow for critical admin operations
-- =============================================
