-- 0008_v1_surface_tables.sql
-- Phase 1 (storage-layer swap) re-adds two tables that Phase 0 had dropped per
-- plan Section 5.2, because the carried-over v1 service surface that PostgresService
-- must mirror still references them:
--   * account_settings    -- read by MT5ManagerConnection / MT5CoverageConnection
--                            factories and the Settings "Connections" UI. Phase 2
--                            removes the Manager connector and may drop this then.
--   * reconciliation_runs  -- written by ReconciliationService, listed by the
--                            Settings ReconciliationCard. Revisit with the Section 5.4
--                            feed<->db self-check decision.
-- Keeping them makes the app fully functional on Postgres without stubbing methods.

-- ------------------------------------------------------------------
-- account_settings  (v1: inline)  PK id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS account_settings (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  account_type  text    NOT NULL,               -- 'manager' | 'coverage'
  label         text    NOT NULL DEFAULT '',
  server        text    NOT NULL DEFAULT '',
  login         bigint  NOT NULL DEFAULT 0,
  password      text    NOT NULL DEFAULT '',
  group_mask    text    NOT NULL DEFAULT '*',
  is_active     boolean NOT NULL DEFAULT true,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS account_settings_type_idx ON account_settings (account_type, created_at);

DROP TRIGGER IF EXISTS trg_account_settings_touch ON account_settings;
CREATE TRIGGER trg_account_settings_touch
  BEFORE UPDATE ON account_settings
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- ------------------------------------------------------------------
-- reconciliation_runs  (v1: 20260418 migration -- reproduced verbatim)  PK id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS reconciliation_runs (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  trigger_type text NOT NULL DEFAULT 'scheduled' CHECK (trigger_type IN ('scheduled','manual')),
  window_from   timestamptz NOT NULL,
  window_to     timestamptz NOT NULL,
  started_at    timestamptz NOT NULL DEFAULT now(),
  finished_at   timestamptz,
  mt5_deal_count      bigint NOT NULL DEFAULT 0,
  supabase_deal_count bigint NOT NULL DEFAULT 0,
  backfilled    bigint NOT NULL DEFAULT 0,
  ghost_deleted bigint NOT NULL DEFAULT 0,
  modified      bigint NOT NULL DEFAULT 0,
  error         text,
  notes         text NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS reconciliation_runs_started_at_idx ON reconciliation_runs (started_at DESC);
