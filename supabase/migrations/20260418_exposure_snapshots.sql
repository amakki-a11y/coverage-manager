-- 20260418_exposure_snapshots.sql
-- Extend `exposure_snapshots` for the Period P&L feature and add a scheduler table.
-- Used by the new "Net P&L" tab to compute FloatingΔ = Current - Begin + Settled.

-- 1. Extend exposure_snapshots with provenance columns and uniqueness for upserts
ALTER TABLE exposure_snapshots
  ADD COLUMN IF NOT EXISTS trigger_type text NOT NULL DEFAULT 'scheduled' CHECK (trigger_type IN ('scheduled','manual','daily','weekly','monthly')),
  ADD COLUMN IF NOT EXISTS label text NOT NULL DEFAULT '';

CREATE UNIQUE INDEX IF NOT EXISTS exposure_snapshots_symbol_time_uidx
  ON exposure_snapshots (canonical_symbol, snapshot_time);

CREATE INDEX IF NOT EXISTS exposure_snapshots_time_idx
  ON exposure_snapshots (snapshot_time DESC);

-- 2. Snapshot scheduler table — drives dealer-configurable cadences
CREATE TABLE IF NOT EXISTS snapshot_schedules (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name text NOT NULL,
  cadence text NOT NULL CHECK (cadence IN ('daily','weekly','monthly','custom')),
  cron_expr text,
  tz text NOT NULL DEFAULT 'Asia/Beirut',
  enabled boolean NOT NULL DEFAULT true,
  last_run_at timestamptz,
  next_run_at timestamptz,
  created_at timestamptz DEFAULT now(),
  updated_at timestamptz DEFAULT now()
);

-- 3. Seed default schedules if the table is empty.
INSERT INTO snapshot_schedules (name, cadence, cron_expr, tz, enabled)
SELECT * FROM (VALUES
  ('Daily Close (Lebanon)',    'daily',   '0 0 * * *',   'Asia/Beirut', true),
  ('Weekly Close (Lebanon)',   'weekly',  '0 0 * * 1',   'Asia/Beirut', true),
  ('Monthly Close (Lebanon)',  'monthly', '0 0 1 * *',   'Asia/Beirut', true)
) AS v(name, cadence, cron_expr, tz, enabled)
WHERE NOT EXISTS (SELECT 1 FROM snapshot_schedules);
