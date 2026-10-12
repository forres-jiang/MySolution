BEGIN;

DO $$
DECLARE
    v_table_name text;
    v_old_name text;
    v_new_name text;
    old_exists boolean;
    new_exists boolean;
BEGIN
    FOREACH v_table_name IN ARRAY ARRAY['Menus', 'RoleMenu'] LOOP
        IF to_regclass(format('public.%I', v_table_name)) IS NULL THEN
            RAISE EXCEPTION 'Required table public.% is missing', v_table_name;
        END IF;
        FOR v_old_name, v_new_name IN
            SELECT * FROM (VALUES ('CreatedTime', 'CreatedAt'), ('UpdatedTime', 'UpdatedAt')) AS names(v_old_name, v_new_name)
        LOOP
            SELECT EXISTS (SELECT 1 FROM information_schema.columns c
                WHERE c.table_schema = 'public' AND c.table_name = v_table_name AND c.column_name = v_old_name),
                EXISTS (SELECT 1 FROM information_schema.columns c
                WHERE c.table_schema = 'public' AND c.table_name = v_table_name AND c.column_name = v_new_name)
            INTO old_exists, new_exists;
            IF old_exists AND NOT new_exists THEN
                EXECUTE format('ALTER TABLE public.%I RENAME COLUMN %I TO %I', v_table_name, v_old_name, v_new_name);
            ELSIF old_exists OR NOT new_exists THEN
                RAISE EXCEPTION 'Ambiguous or missing audit column on public.%: % -> %', v_table_name, v_old_name, v_new_name;
            END IF;
        END LOOP;
    END LOOP;
END $$;

COMMIT;
