-- SQL Server Script for Identity Module

-- Create Permissions Table
CREATE TABLE [Permissions] (
    [PermissionId] INT NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([PermissionId])
);
GO

-- Create Roles Table
CREATE TABLE [Roles] (
    [RoleId] INT NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
);
GO

-- Create Users Table
CREATE TABLE [Users] (
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [Email] NVARCHAR(256) NOT NULL,
    [HashedPassword] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
);
GO
CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
GO

-- Create UserRefreshTokens Table
CREATE TABLE [UserRefreshTokens] (
    [UserRefreshTokenId] INT IDENTITY(1,1) NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [Token] NVARCHAR(MAX) NOT NULL,
    [Expires] DATETIME2 NOT NULL,
    [Created] DATETIME2 NOT NULL,
    CONSTRAINT [PK_UserRefreshTokens] PRIMARY KEY ([UserRefreshTokenId]),
    CONSTRAINT [FK_UserRefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);
GO

-- Create UserRoles Junction Table
CREATE TABLE [UserRoles] (
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [RoleId] INT NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE CASCADE
);
GO

-- Create RolePermissions Junction Table
CREATE TABLE [RolePermissions] (
    [RoleId] INT NOT NULL,
    [PermissionId] INT NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
    CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]) ON DELETE CASCADE,
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([PermissionId]) ON DELETE CASCADE
);
GO

-- Seed Data
INSERT INTO [Permissions] ([PermissionId], [Name]) VALUES
(1, 'Read'),
(2, 'Create'),
(3, 'Update'),
(4, 'Delete');
GO

INSERT INTO [Roles] ([RoleId], [Name]) VALUES
(1, 'Admin'),
(2, 'Employee');
GO
