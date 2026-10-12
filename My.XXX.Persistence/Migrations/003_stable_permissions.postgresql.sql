-- Stop old writers. Extend this mapping for custom protected endpoints before deployment.
BEGIN;
DO $$
BEGIN
    IF to_regclass('public."RolePermissions"') IS NULL THEN
        CREATE TEMP TABLE permission_migration_map (legacy_path text PRIMARY KEY, code varchar(100) NOT NULL) ON COMMIT DROP;
        INSERT INTO permission_migration_map VALUES
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
        IF EXISTS (SELECT 1 FROM public."Menus" m JOIN public."RoleMenu" rm ON rm."MenuId"=m."Id"
            WHERE NOT m."IsDeleted" AND NOT rm."IsDeleted" AND m."IsAction"
            AND NOT EXISTS (SELECT 1 FROM permission_migration_map p WHERE p.legacy_path=lower(
                CASE WHEN nullif(m."ControllerName",'') IS NOT NULL AND nullif(m."ActionName",'') IS NOT NULL
                    THEN m."ControllerName"||'/'||m."ActionName" ELSE m."Url" END))) THEN
            RAISE EXCEPTION 'Unmapped action grants. Extend the stable permission catalog and migration mapping first.';
        END IF;
        CREATE TABLE public."RolePermissions" ("RoleId" uuid NOT NULL, "Code" varchar(100) NOT NULL,
            PRIMARY KEY ("RoleId", "Code"));
        INSERT INTO public."RolePermissions"("RoleId", "Code")
            SELECT DISTINCT rm."RoleId", p.code FROM public."RoleMenu" rm JOIN public."Menus" m ON m."Id"=rm."MenuId"
            JOIN permission_migration_map p ON p.legacy_path=lower(CASE
                WHEN nullif(m."ControllerName",'') IS NOT NULL AND nullif(m."ActionName",'') IS NOT NULL
                    THEN m."ControllerName"||'/'||m."ActionName" ELSE m."Url" END)
            WHERE NOT rm."IsDeleted" AND NOT m."IsDeleted";
        UPDATE public."PermissionRevision" SET "Version"="Version"+1 WHERE "Id"=1;
    END IF;
END $$;
COMMIT;
