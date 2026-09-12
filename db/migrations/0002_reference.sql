-- 0002_reference.sql
-- Reference / roster tables: symbol_mappings, trading_accounts, moved_accounts.
-- Columns and conflict keys reproduce v1's live Supabase schema exactly so that a
-- verbatim row-for-row import (db/import) cannot be rejected by a stricter v2
-- constraint. Money/volume are unbounded `numeric` to match v1's bare-numeric columns.

-- ------------------------------------------------------------------
-- symbol_mappings  (v1: inline base table + pip_size from 20260416 migration)
-- B-Book <-> LP mapping, contract sizes, per-symbol pip override.
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS symbol_mappings (
  id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  canonical_name          text    NOT NULL,
  bbook_symbol            text    NOT NULL,
  bbook_contract_size     numeric NOT NULL,
  coverage_symbol         text    NOT NULL,
  coverage_contract_size  numeric NOT NULL,
  digits                  integer NOT NULL,
  profit_currency         text    NOT NULL DEFAULT 'USD',
  is_active               boolean NOT NULL DEFAULT true,
  pip_size                numeric            -- NULL => BridgePipResolver falls back to heuristics
);
-- Lookups. NOT unique: v1 keeps multiple rows per canonical_name (one per raw
-- symbol variant, e.g. UT100-20 / UT100.c), and the app upserts on the uuid PK.
CREATE INDEX IF NOT EXISTS symbol_mappings_canonical_idx ON symbol_mappings (canonical_name);
CREATE INDEX IF NOT EXISTS symbol_mappings_bbook_idx     ON symbol_mappings (bbook_symbol);
CREATE INDEX IF NOT EXISTS symbol_mappings_coverage_idx  ON symbol_mappings (coverage_symbol);

COMMENT ON COLUMN symbol_mappings.pip_size IS
  'Pip size used by the Bridge tab pip-conversion. NULL => resolver falls back to symbol-name + price heuristics.';

-- ------------------------------------------------------------------
-- trading_accounts  (v1: inline)  conflict key (source, login)
-- Mirror of every account. In v2 this is populated from Live Bridge account frames,
-- not a Manager roster poll -- but the shape is unchanged so history imports cleanly.
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS trading_accounts (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source             text    NOT NULL DEFAULT 'bbook',
  login              bigint  NOT NULL,
  name               text    NOT NULL DEFAULT '',
  group_name         text    NOT NULL DEFAULT '',
  leverage           integer NOT NULL DEFAULT 0,
  balance            numeric NOT NULL DEFAULT 0,
  equity             numeric NOT NULL DEFAULT 0,
  credit             numeric NOT NULL DEFAULT 0,
  margin             numeric NOT NULL DEFAULT 0,
  free_margin        numeric NOT NULL DEFAULT 0,
  currency           text    NOT NULL DEFAULT 'USD',
  registration_time  timestamptz,
  last_trade_time    timestamptz,
  status             text    NOT NULL DEFAULT 'active',
  comment            text    NOT NULL DEFAULT '',
  synced_at          timestamptz NOT NULL DEFAULT now(),
  created_at         timestamptz NOT NULL DEFAULT now(),
  updated_at         timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS trading_accounts_source_login_uk ON trading_accounts (source, login);
CREATE INDEX IF NOT EXISTS trading_accounts_login_idx ON trading_accounts (login);

DROP TRIGGER IF EXISTS trg_trading_accounts_touch ON trading_accounts;
CREATE TRIGGER trg_trading_accounts_touch
  BEFORE UPDATE ON trading_accounts
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- ------------------------------------------------------------------
-- moved_accounts  (v1: inline, read-only in code)
-- Logins removed from the source feed but whose deals are kept for history and
-- excluded from the live dashboard. Only `login` and `moved_at` are referenced in
-- code; extra columns (reason/moved_by) may exist in v1 and are added by the import
-- verifier if the source carries them (see db/import). PK on login (a set of logins).
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS moved_accounts (
  login     bigint PRIMARY KEY,
  moved_at  timestamptz NOT NULL DEFAULT now(),
  reason    text,
  moved_by  text
);
