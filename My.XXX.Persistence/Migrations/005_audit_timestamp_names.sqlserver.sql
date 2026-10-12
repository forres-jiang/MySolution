SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @tableName sysname, @oldName sysname, @newName sysname;
DECLARE @objectName nvarchar(776), @columnName nvarchar(776);
DECLARE audit_columns CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.table_name, c.old_name, c.new_name
    FROM (VALUES (N'Menus'), (N'RoleMenu')) AS t(table_name)
    CROSS JOIN (VALUES (N'CreatedTime', N'CreatedAt'), (N'UpdatedTime', N'UpdatedAt')) AS c(old_name, new_name);

OPEN audit_columns;
FETCH NEXT FROM audit_columns INTO @tableName, @oldName, @newName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @objectName = N'dbo.' + QUOTENAME(@tableName);
    IF OBJECT_ID(@objectName, N'U') IS NULL
        THROW 51000, 'Required audit table is missing.', 1;

    IF COL_LENGTH(@objectName, @oldName) IS NOT NULL AND COL_LENGTH(@objectName, @newName) IS NULL
    BEGIN
        SET @columnName = @objectName + N'.' + QUOTENAME(@oldName);
        EXEC sys.sp_rename @objname = @columnName, @newname = @newName, @objtype = N'COLUMN';
    END
    ELSE IF COL_LENGTH(@objectName, @oldName) IS NOT NULL OR COL_LENGTH(@objectName, @newName) IS NULL
        THROW 51000, 'Ambiguous or missing audit column; expected the old or new column only.', 1;

    FETCH NEXT FROM audit_columns INTO @tableName, @oldName, @newName;
END;
CLOSE audit_columns;
DEALLOCATE audit_columns;

COMMIT TRANSACTION;
