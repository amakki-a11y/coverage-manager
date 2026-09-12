-- 0001_bootstrap.sql
-- Coverage Manager v2 -- local PostgreSQL store.
-- Shared helpers. Forward-only; do not edit once applied (add a new migration).
--
-- Notes:
--   * gen_random_uuid() is a core function in PostgreSQL 13+, so no pgcrypto
--     extension is required for the uuid defaults used throughout v2.
--   * Roles and the database itself are created by db/bootstrap-db.ps1 BEFORE
--     migrations run -- role/database creation does not belong in a migration
--     that executes inside the target database.

-- Shared "keep updated_at fresh on UPDATE" trigger function.
-- v1 defined this per-domain (touch_updated_at / bridge_executions_touch_updated_at);
-- v2 consolidates to one reusable function.
CREATE OR REPLACE FUNCTION touch_updated_at()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$$;
