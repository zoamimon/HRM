-- SQL Server Script for Personnel Module

-- Create Employees Table
CREATE TABLE [Employees] (
    [EmployeeId] UNIQUEIDENTIFIER NOT NULL,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName] NVARCHAR(100) NOT NULL,
    [Email] NVARCHAR(256) NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([EmployeeId])
);
GO
CREATE UNIQUE INDEX [IX_Employees_Email] ON [Employees] ([Email]);
GO

-- Create EmployeeCompanyAssignments Table
CREATE TABLE [EmployeeCompanyAssignments] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [EmployeeId] UNIQUEIDENTIFIER NOT NULL,
    [CompanyId] UNIQUEIDENTIFIER NOT NULL,
    [DepartmentId] UNIQUEIDENTIFIER NOT NULL,
    [PositionId] UNIQUEIDENTIFIER NOT NULL,
    [IsPrimaryRole] BIT NOT NULL,
    [StartDate] DATETIME2 NOT NULL,
    [EndDate] DATETIME2 NULL,
    CONSTRAINT [PK_EmployeeCompanyAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeCompanyAssignments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([EmployeeId]) ON DELETE CASCADE
);
GO
-- Note: Foreign keys to Company, Department, and Position tables are not enforced
-- at the database level because they belong to a different database (Bounded Context).
-- This integrity must be maintained by the Application layer.

-- Create OutboxMessages Table
CREATE TABLE [OutboxMessages] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OccurredOnUtc] DATETIME2 NOT NULL,
    [Type] NVARCHAR(MAX) NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL,
    [ProcessedDateUtc] DATETIME2 NULL,
    CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
);
GO
