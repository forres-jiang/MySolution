-- Deploy with older application instances stopped: they still use PermissionRevision for menu locking.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.MenuWriteLock', N'U') IS NULL
    CREATE TABLE dbo.MenuWriteLock (Id int NOT NULL PRIMARY KEY CHECK (Id = 1), Version bigint NOT NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.MenuWriteLock WHERE Id = 1)
    INSERT INTO dbo.MenuWriteLock (Id, Version) VALUES (1, 0);
COMMIT;
