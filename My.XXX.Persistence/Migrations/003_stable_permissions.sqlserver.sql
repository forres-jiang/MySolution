-- Stop old writers. Extend this mapping for custom protected endpoints before deployment.
-- Only the first run imports grants. Subsequent runs do not change permissions.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    DECLARE @mapping TABLE (LegacyPath nvarchar(300) PRIMARY KEY, Code varchar(100) NOT NULL);
    INSERT INTO @mapping VALUES
        ('menu/add', 'menu.add'),
        ('menu/remove', 'menu.remove'),
        ('menu/edit', 'menu.edit'),
        ('menu/get', 'menu.get'),
        ('menu/list', 'menu.list'),
        ('menu/search', 'menu.search'),
        ('menu/rolemenu', 'menu.role-menu'),
        ('menu/rolemenus', 'menu.role-menus'),
        ('menu/rolemenuchecked', 'menu.role-menu-checked'),
        ('menu/removerolemenu', 'menu.remove-role-menu'),
        ('menu/tree', 'menu.tree'),
        ('menu/displaytree', 'menu.display-tree'),
        ('menu/getmenutreebyroleid', 'menu.tree-by-role-id'),
        ('menu/getmenutreebyroleids', 'menu.tree-by-role-ids'),
        ('menu/updatesort', 'menu.update-sort'),
        ('operation/list', 'operation.list');
    IF EXISTS (SELECT 1 FROM dbo.Menus m JOIN dbo.RoleMenu rm ON rm.MenuId=m.Id
        WHERE m.IsDeleted=0 AND rm.IsDeleted=0 AND m.IsAction=1
        AND NOT EXISTS (SELECT 1 FROM @mapping p WHERE p.LegacyPath=LOWER(
            CASE WHEN NULLIF(m.ControllerName,'') IS NOT NULL AND NULLIF(m.ActionName,'') IS NOT NULL
                THEN m.ControllerName+'/'+m.ActionName ELSE m.Url END)))
        THROW 51000, 'Unmapped action grants. Extend the stable permission catalog and migration mapping first.', 1;
    CREATE TABLE dbo.RolePermissions (RoleId uniqueidentifier NOT NULL, Code varchar(100) NOT NULL,
        CONSTRAINT PK_RolePermissions PRIMARY KEY(RoleId, Code));
    INSERT INTO dbo.RolePermissions(RoleId, Code)
        SELECT DISTINCT rm.RoleId, p.Code FROM dbo.RoleMenu rm JOIN dbo.Menus m ON m.Id=rm.MenuId
        JOIN @mapping p ON p.LegacyPath=LOWER(CASE
            WHEN NULLIF(m.ControllerName,'') IS NOT NULL AND NULLIF(m.ActionName,'') IS NOT NULL
                THEN m.ControllerName+'/'+m.ActionName ELSE m.Url END)
        WHERE rm.IsDeleted=0 AND m.IsDeleted=0;
    UPDATE dbo.PermissionRevision SET Version=Version+1 WHERE Id=1;
END;
COMMIT;
