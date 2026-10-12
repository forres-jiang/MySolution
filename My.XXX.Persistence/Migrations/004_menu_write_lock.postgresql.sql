-- Deploy with older application instances stopped: they still use PermissionRevision for menu locking.
BEGIN;
CREATE TABLE IF NOT EXISTS public."MenuWriteLock" (
    "Id" integer PRIMARY KEY CHECK ("Id" = 1), "Version" bigint NOT NULL
);
INSERT INTO public."MenuWriteLock" ("Id", "Version") VALUES (1, 0) ON CONFLICT ("Id") DO NOTHING;
COMMIT;
