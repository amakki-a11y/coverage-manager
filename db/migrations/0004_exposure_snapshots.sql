-- 0004_exposure_snapshots.sql
-- exposure_snapshots (Net P&L "Begin" anchor) + snapshot_schedules (scheduler cadences).
-- v1 created the exposure_snapshots BASE table inline and only ALTER-ed in
-- trigger_type/label via 20260418; v2 folds the full definition into one migration.

-- ------------------------------------------------------------------
-- exposure_snapshots  conflict key (canonical_symbol, snapshot_time)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS exposure_snapshots (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  canonical_symbol      text NOT NULL,
  snapshot_time         timestamptz NOT NULL,
  bbook_buy_volume      numeric NOT NULL DEFAULT 0,
  bbook_sell_volume     numeric NOT NULL DEFAULT 0,
  coverage_buy_volume   numeric NOT NULL DEFAULT 0,
  coverage_sell_volume  numeric NOT NULL DEFAULT 0,
  net_volume            numeric NOT NULL DEFAULT 0,
  bbook_pnl             numeric NOT NULL DEFAULT 0,
  coverage_pnl          numeric NOT NULL DEFAULT 0,
  net_pnl               numeric NOT NULL DEFAULT 0,
  trigger_type          text NOT NULL DEFAULT 'scheduled'
                          CHECK (trigger_type IN ('scheduled','manual','daily','weekly','monthly')),
  label                 text NOT NULL DEFAULT ''
);
CREATE UNIQUE INDEX IF NOT EXISTS exposure_snapshots_symbol_time_uidx
  ON exposure_snapshots (canonical_symbol, snapshot_time);
CREATE INDEX IF NOT EXISTS exposure_snapshots_time_idx
  ON exposure_snapshots (snapshot_time DESC);

-- ------------------------------------------------------------------
-- snapshot_schedules  (v1: 20260418 migration -- reproduced verbatim incl. seed)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS snapshot_schedules (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name         text NOT NULL,
  cadence      text NOT NULL CHECK (cadence IN ('daily','weekly','monthly','custom')),
  cron_expr    text,
  tz           text NOT NULL DEFAULT 'Asia/Beirut',
  enabled      boolean NOT NULL DEFAULT true,
  last_run_at  timestamptz,
  next_run_at  timestamptz,
  created_at   timestamptz DEFAULT now(),
  updated_at   timestamptz DEFAULT now()
);

-- Seed default schedules only if the table is empty. (The real import may replace
-- these with the v1 rows; the seed just guarantees a working scheduler on a fresh DB.)
INSERT INTO snapshot_schedules (name, cadence, cron_expr, tz, enabled)
SELECT * FROM (VALUES
  ('Daily Close (Lebanon)',   'daily',   '0 0 * * *', 'Asia/Beirut', true),
  ('Weekly Close (Lebanon)',  'weekly',  '0 0 * * 1', 'Asia/Beirut', true),
  ('Monthly Close (Lebanon)', 'monthly', '0 0 1 * *', 'Asia/Beirut', true)
) AS v(name, cadence, cron_expr, tz, enabled)
WHERE NOT EXISTS (SELECT 1 FROM snapshot_schedules);
