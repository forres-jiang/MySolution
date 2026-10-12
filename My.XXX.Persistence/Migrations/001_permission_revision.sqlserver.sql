-- Run on Default before deployment. Stop older application instances first.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.PermissionRevision', N'U') IS NULL
    CREATE TABLE dbo.PermissionRevision (Id int NOT NULL PRIMARY KEY CHECK (Id = 1), Version bigint NOT NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.PermissionRevision WHERE Id = 1)
    INSERT INTO dbo.PermissionRevision (Id, Version) VALUES (1, 0);
-- Existing duplicate active relations must be repaired explicitly before upgrading.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_RoleMenu_Active' AND object_id = OBJECT_ID(N'dbo.RoleMenu'))
    CREATE UNIQUE INDEX UX_RoleMenu_Active ON dbo.RoleMenu(RoleId, MenuId) WHERE IsDeleted = 0;
COMMIT;
