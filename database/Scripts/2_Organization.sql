-- SQL Server Script for Organization Module

-- Create Companies Table
CREATE TABLE [Companies] (
    [CompanyId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [ParentId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_Companies] PRIMARY KEY ([CompanyId]),
    CONSTRAINT [FK_Companies_Companies_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Companies] ([CompanyId]) ON DELETE NO ACTION
);
GO

-- Create Departments Table
CREATE TABLE [Departments] (
    [DepartmentId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [CompanyId] UNIQUEIDENTIFIER NOT NULL,
    [ParentId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([DepartmentId]),
    CONSTRAINT [FK_Departments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([CompanyId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Departments_Departments_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Departments] ([DepartmentId]) ON DELETE NO ACTION
);
GO

-- Create Positions Table
CREATE TABLE [Positions] (
    [PositionId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [DepartmentId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_Positions] PRIMARY KEY ([PositionId]),
    CONSTRAINT [FK_Positions_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([DepartmentId]) ON DELETE CASCADE
);
GO
