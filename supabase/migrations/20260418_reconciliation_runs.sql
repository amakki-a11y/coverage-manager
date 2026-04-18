-- 20260418_reconciliation_runs.sql
-- Audit table for the nightly ReconciliationService sweep that deletes ghost deals
-- (deals in Supabase but no longer in MT5 Manager) and propagates modifications.
-- Surfaced to the dealer in Settings → Reconciliation.

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
