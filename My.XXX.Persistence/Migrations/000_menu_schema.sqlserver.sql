-- Bootstrap empty databases; existing installations keep their data and audit column names.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Menus', N'U') IS NULL
    CREATE TABLE dbo.Menus (
        Id int IDENTITY(1,1) PRIMARY KEY,
        DisplayName nvarchar(max) NULL,
        Description nvarchar(max) NULL,
        Icon nvarchar(max) NULL,
        Number int NOT NULL,
        Url nvarchar(max) NULL,
        Component nvarchar(max) NULL,
        ControllerName nvarchar(max) NULL,
        ActionName nvarchar(max) NULL,
        LinkTarget nvarchar(max) NULL,
        ParentId int NOT NULL,
        IsDisplay bit NOT NULL,
        IsAction bit NOT NULL,
        IsDeleted bit NOT NULL,
        CreatedBy nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedBy nvarchar(max) NULL,
        UpdatedAt datetime2 NULL
    );
IF OBJECT_ID(N'dbo.RoleMenu', N'U') IS NULL
    CREATE TABLE dbo.RoleMenu (
        Id int IDENTITY(1,1) PRIMARY KEY,
        RoleId uniqueidentifier NOT NULL,
        MenuId int NOT NULL,
        IsDeleted bit NOT NULL,
        CreatedBy nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedBy nvarchar(max) NULL,
        UpdatedAt datetime2 NULL
    );
COMMIT TRANSACTION;
