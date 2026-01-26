-- =============================================
-- Script: Create Indexes for Operators Table
-- Module: Identity
-- Purpose: Create non-clustered indexes for performance optimization
-- Dependencies: 001_CreateOperatorsTable.sql
-- =============================================

USE HrmDb
GO

-- Drop existing indexes if they exist (for development only - remove in production)
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Operators_Username' AND object_id = OBJECT_ID('[Identity].Operators'))
    DROP INDEX IX_Operators_Username ON [Identity].Operators
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Operators_Email' AND object_id = OBJECT_ID('[Identity].Operators'))
    DROP INDEX IX_Operators_Email ON [Identity].Operators
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Operators_Status' AND object_id = OBJECT_ID('[Identity].Operators'))
    DROP INDEX IX_Operators_Status ON [Identity].Operators
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Operators_CreatedAtUtc' AND object_id = OBJECT_ID('[Identity].Operators'))
    DROP INDEX IX_Operators_CreatedAtUtc ON [Identity].Operators
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Operators_IsDeleted' AND object_id = OBJECT_ID('[Identity].Operators'))
    DROP INDEX IX_Operators_IsDeleted ON [Identity].Operators
GO

-- =============================================
-- Index 1: Username (Unique, for login queries)
-- =============================================
-- Purpose: Fast lookup by username during login
-- Use Case: Login endpoint, username uniqueness check
-- Performance: Index Seek (O(log n))
-- Includes: PasswordHash, Status, IsDeleted (avoid key lookups)
CREATE UNIQUE NONCLUSTERED INDEX IX_Operators_Username
ON [Identity].Operators (Username)
INCLUDE (PasswordHash, Status, IsDeleted, FailedLoginAttempts, LockedUntilUtc)
WHERE IsDeleted = 0
GO

PRINT 'Index IX_Operators_Username created successfully'
GO

-- =============================================
-- Index 2: Email (Unique, for uniqueness checks)
-- =============================================
-- Purpose: Fast lookup by email, uniqueness validation
-- Use Case: Registration validation, forgot password
-- Performance: Index Seek (O(log n))
CREATE UNIQUE NONCLUSTERED INDEX IX_Operators_Email
ON [Identity].Operators (Email)
INCLUDE (Id, Username, FullName)
WHERE IsDeleted = 0
GO

PRINT 'Index IX_Operators_Email created successfully'
GO

-- =============================================
-- Index 3: Status (for filtering by status)
-- =============================================
-- Purpose: Fast filtering by operator status
-- Use Case: Admin dashboard (list pending/active operators)
-- Performance: Index Seek + Range Scan
-- Example Query: SELECT * FROM Operators WHERE Status = 0 (Pending)
CREATE NONCLUSTERED INDEX IX_Operators_Status
ON [Identity].Operators (Status, CreatedAtUtc DESC)
INCLUDE (Id, Username, Email, FullName)
WHERE IsDeleted = 0
GO

PRINT 'Index IX_Operators_Status created successfully'
GO

-- =============================================
-- Index 4: CreatedAtUtc (for sorting/pagination)
-- =============================================
-- Purpose: Fast chronological sorting and pagination
-- Use Case: List operators sorted by registration date
-- Performance: Index Scan (already sorted)
-- Example Query: SELECT * FROM Operators ORDER BY CreatedAtUtc DESC OFFSET 20 ROWS FETCH NEXT 20 ROWS ONLY
CREATE NONCLUSTERED INDEX IX_Operators_CreatedAtUtc
ON [Identity].Operators (CreatedAtUtc DESC)
INCLUDE (Id, Username, Email, FullName, Status)
WHERE IsDeleted = 0
GO

PRINT 'Index IX_Operators_CreatedAtUtc created successfully'
GO

-- =============================================
-- Index 5: IsDeleted (for soft delete queries)
-- =============================================
-- Purpose: Fast filtering of deleted records (if needed)
-- Use Case: Admin viewing deleted operators, restore operations
-- Performance: Index Seek
-- Note: Most queries use WHERE IsDeleted = 0 (filtered indexes above)
CREATE NONCLUSTERED INDEX IX_Operators_IsDeleted
ON [Identity].Operators (IsDeleted, DeletedAtUtc DESC)
INCLUDE (Id, Username, Email, FullName)
GO

PRINT 'Index IX_Operators_IsDeleted created successfully'
GO

-- =============================================
-- Index Statistics and Analysis
-- =============================================
-- Update statistics for accurate query plans
UPDATE STATISTICS [Identity].Operators
GO

-- Display index information
SELECT
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_unique AS IsUnique,
    i.fill_factor AS FillFactor,
    CAST(SUM(s.used_page_count) * 8.0 / 1024 AS DECIMAL(10, 2)) AS SizeMB
FROM sys.indexes i
INNER JOIN sys.dm_db_partition_stats s
    ON i.object_id = s.object_id
    AND i.index_id = s.index_id
WHERE i.object_id = OBJECT_ID('[Identity].Operators')
GROUP BY i.name, i.type_desc, i.is_unique, i.fill_factor
ORDER BY i.name
GO

PRINT 'All indexes created successfully'
PRINT 'Run this script after 001_CreateOperatorsTable.sql and before 003_SeedAdminOperator.sql'
GO
