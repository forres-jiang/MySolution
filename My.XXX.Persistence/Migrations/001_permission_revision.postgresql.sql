-- Run on Default before deployment. Stop older application instances first.
BEGIN;
CREATE TABLE IF NOT EXISTS public."PermissionRevision" (
    "Id" integer PRIMARY KEY CHECK ("Id" = 1), "Version" bigint NOT NULL
);
INSERT INTO public."PermissionRevision" ("Id", "Version") VALUES (1, 0) ON CONFLICT ("Id") DO NOTHING;
-- Existing duplicate active relations must be repaired explicitly before upgrading.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_RoleMenu_Active" ON public."RoleMenu" ("RoleId", "MenuId") WHERE NOT "IsDeleted";
COMMIT;
