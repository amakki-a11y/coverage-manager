-- ========================================================================
-- Phase 1 — Equity P&L tab: per-login equity snapshots + PS/rebate config
-- ========================================================================
-- Three tables:
--   account_equity_snapshots   — point-in-time (login, equity, balance, credit)
--                                  captured by the existing scheduler that
--                                  already feeds exposure_snapshots.
--   equity_pnl_client_config   — per-login rebate % + PS % + running
--                                  low-water-mark state for the PS engine.
--   equity_pnl_spread_rebates  — per-login per-symbol $/lot spread rebate.
-- ========================================================================

-- A. Snapshot table (per-login equity)
CREATE TABLE IF NOT EXISTS account_equity_snapshots (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  login           bigint       NOT NULL,
  source          text         NOT NULL,                     -- 'bbook' | 'coverage'
  snapshot_time   timestamptz  NOT NULL,
  balance         numeric(18,2) NOT NULL,
  equity          numeric(18,2) NOT NULL,
  credit          numeric(18,2) NOT NULL DEFAULT 0,
  margin          numeric(18,2),
  trigger_type    text         NOT NULL DEFAULT 'scheduled', -- scheduled | manual | daily | weekly | monthly
  label           text,
  created_at      timestamptz  NOT NULL DEFAULT now()
);

-- Idempotent capture — if a schedule fires twice (e.g. manual during scheduler run)
-- we keep only the first write for that (login, source, snapshot_time).
CREATE UNIQUE INDEX IF NOT EXISTS account_equity_snapshots_uk
  ON account_equity_snapshots (login, source, snapshot_time);

-- Fast "latest snapshot before X" lookups (per-login).
CREATE INDEX IF NOT EXISTS account_equity_snapshots_login_time_idx
  ON account_equity_snapshots (login, snapshot_time DESC);

-- Fast "all snapshots in range" lookups (for PS month walks).
CREATE INDEX IF NOT EXISTS account_equity_snapshots_time_idx
  ON account_equity_snapshots (snapshot_time DESC);


-- B. Per-login PS + commission rebate configuration.
-- One row per (login, source). Dealer edits these via the Settings tab.
CREATE TABLE IF NOT EXISTS equity_pnl_client_config (
  login                    bigint       NOT NULL,
  source                   text         NOT NULL,
  comm_rebate_pct          numeric(6,3) NOT NULL DEFAULT 0,   -- 50.000 = 50 %
  ps_pct                   numeric(6,3) NOT NULL DEFAULT 0,   -- 10.000 = 10 %
  ps_contract_start        date,                              -- null = PS disabled
  ps_cum_pl                numeric(18,2) NOT NULL DEFAULT 0,  -- running cum trading P&L
  ps_low_water_mark        numeric(18,2) NOT NULL DEFAULT 0,  -- most-negative cum_pl seen
  ps_last_processed_month  date,                              -- prevents double-pay on reruns
  notes                    text,
  updated_at               timestamptz  NOT NULL DEFAULT now(),
  PRIMARY KEY (login, source)
);


-- C. Per-login per-symbol spread rebate rates.
-- Canonical symbol, matches the naming used elsewhere in the app.
CREATE TABLE IF NOT EXISTS equity_pnl_spread_rebates (
  login              bigint       NOT NULL,
  source             text         NOT NULL,
  canonical_symbol   text         NOT NULL,
  rate_per_lot       numeric(10,4) NOT NULL,                  -- e.g. 5.0000 USD per lot
  updated_at         timestamptz  NOT NULL DEFAULT now(),
  PRIMARY KEY (login, source, canonical_symbol)
);


-- Trigger to keep updated_at current on upsert for the config tables.
CREATE OR REPLACE FUNCTION touch_updated_at()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_equity_pnl_client_config_touch ON equity_pnl_client_config;
CREATE TRIGGER trg_equity_pnl_client_config_touch
  BEFORE UPDATE ON equity_pnl_client_config
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

DROP TRIGGER IF EXISTS trg_equity_pnl_spread_rebates_touch ON equity_pnl_spread_rebates;
CREATE TRIGGER trg_equity_pnl_spread_rebates_touch
  BEFORE UPDATE ON equity_pnl_spread_rebates
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();
