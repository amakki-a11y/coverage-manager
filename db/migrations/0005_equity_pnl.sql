-- 0005_equity_pnl.sql
-- Equity P&L tab: per-login equity snapshots, per-login rebate/PS config, spread
-- rebates, and the Phase-2 login-group tables. Precisions match v1 exactly.

-- ------------------------------------------------------------------
-- account_equity_snapshots  (v1: 20260419)  conflict key (login, source, snapshot_time)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS account_equity_snapshots (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  login          bigint       NOT NULL,
  source         text         NOT NULL,
  snapshot_time  timestamptz  NOT NULL,
  balance        numeric(18,2) NOT NULL,
  equity         numeric(18,2) NOT NULL,
  credit         numeric(18,2) NOT NULL DEFAULT 0,
  margin         numeric(18,2),
  trigger_type   text         NOT NULL DEFAULT 'scheduled',
  label          text,
  created_at     timestamptz  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS account_equity_snapshots_uk
  ON account_equity_snapshots (login, source, snapshot_time);
CREATE INDEX IF NOT EXISTS account_equity_snapshots_login_time_idx
  ON account_equity_snapshots (login, snapshot_time DESC);
CREATE INDEX IF NOT EXISTS account_equity_snapshots_time_idx
  ON account_equity_snapshots (snapshot_time DESC);

-- ------------------------------------------------------------------
-- equity_pnl_client_config  (v1: 20260419)  PK (login, source)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS equity_pnl_client_config (
  login                    bigint        NOT NULL,
  source                   text          NOT NULL,
  comm_rebate_pct          numeric(6,3)  NOT NULL DEFAULT 0,
  ps_pct                   numeric(6,3)  NOT NULL DEFAULT 0,
  ps_contract_start        date,
  ps_cum_pl                numeric(18,2) NOT NULL DEFAULT 0,
  ps_low_water_mark        numeric(18,2) NOT NULL DEFAULT 0,
  ps_last_processed_month  date,
  notes                    text,
  updated_at               timestamptz   NOT NULL DEFAULT now(),
  PRIMARY KEY (login, source)
);
DROP TRIGGER IF EXISTS trg_equity_pnl_client_config_touch ON equity_pnl_client_config;
CREATE TRIGGER trg_equity_pnl_client_config_touch
  BEFORE UPDATE ON equity_pnl_client_config
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- ------------------------------------------------------------------
-- equity_pnl_spread_rebates  (v1: 20260419)  PK (login, source, canonical_symbol)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS equity_pnl_spread_rebates (
  login             bigint        NOT NULL,
  source            text          NOT NULL,
  canonical_symbol  text          NOT NULL,
  rate_per_lot      numeric(10,4) NOT NULL,
  updated_at        timestamptz   NOT NULL DEFAULT now(),
  PRIMARY KEY (login, source, canonical_symbol)
);
DROP TRIGGER IF EXISTS trg_equity_pnl_spread_rebates_touch ON equity_pnl_spread_rebates;
CREATE TRIGGER trg_equity_pnl_spread_rebates_touch
  BEFORE UPDATE ON equity_pnl_spread_rebates
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- ------------------------------------------------------------------
-- login_groups  (v1: inline, Phase 2)  PK id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS login_groups (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name        text NOT NULL,
  description text,
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS login_groups_name_uk ON login_groups (name);

-- ------------------------------------------------------------------
-- login_group_members  (v1: inline, Phase 2)  conflict key (group_id, login, source)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS login_group_members (
  group_id  uuid   NOT NULL REFERENCES login_groups(id) ON DELETE CASCADE,
  login     bigint NOT NULL,
  source    text   NOT NULL DEFAULT 'bbook',
  priority  integer NOT NULL DEFAULT 0,
  added_at  timestamptz DEFAULT now(),
  PRIMARY KEY (group_id, login, source)
);
CREATE INDEX IF NOT EXISTS login_group_members_login_idx ON login_group_members (login, source);

-- ------------------------------------------------------------------
-- equity_pnl_group_config  (v1: inline, Phase 2)  PK group_id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS equity_pnl_group_config (
  group_id         uuid PRIMARY KEY REFERENCES login_groups(id) ON DELETE CASCADE,
  comm_rebate_pct  numeric(6,3) NOT NULL DEFAULT 0,
  ps_pct           numeric(6,3) NOT NULL DEFAULT 0,
  notes            text,
  updated_at       timestamptz DEFAULT now()
);
DROP TRIGGER IF EXISTS trg_equity_pnl_group_config_touch ON equity_pnl_group_config;
CREATE TRIGGER trg_equity_pnl_group_config_touch
  BEFORE UPDATE ON equity_pnl_group_config
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- ------------------------------------------------------------------
-- equity_pnl_group_spread_rebates  (v1: inline, Phase 2)  conflict key (group_id, canonical_symbol)
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS equity_pnl_group_spread_rebates (
  group_id          uuid NOT NULL REFERENCES login_groups(id) ON DELETE CASCADE,
  canonical_symbol  text NOT NULL,
  rate_per_lot      numeric(10,4) NOT NULL,
  updated_at        timestamptz DEFAULT now(),
  PRIMARY KEY (group_id, canonical_symbol)
);
