-- 0003_deals_positions.sql
-- deals (the 280K+ archive), positions (v2-new persisted snapshot), trade_audit_log.

-- ------------------------------------------------------------------
-- deals  (v1: inline)  conflict key (source, deal_id)
-- Volume includes IN+OUT deals; P&L only from OUT deals; commission/fee from all.
-- Balance/credit deals (action >= 2) are KEPT so Equity P&L can source cash movement.
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS deals (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source            text    NOT NULL DEFAULT 'bbook',
  deal_id           bigint  NOT NULL,
  login             bigint  NOT NULL,
  symbol            text    NOT NULL DEFAULT '',
  canonical_symbol  text    NOT NULL DEFAULT '',
  direction         text    NOT NULL DEFAULT '',
  action            integer NOT NULL DEFAULT 0,   -- 0 BUY,1 SELL,2 BALANCE,3 CREDIT,...
  entry             integer NOT NULL DEFAULT 0,   -- 0 IN,1 OUT,2 INOUT,3 OUT_BY
  volume            numeric NOT NULL DEFAULT 0,
  price             numeric NOT NULL DEFAULT 0,
  profit            numeric NOT NULL DEFAULT 0,
  commission        numeric NOT NULL DEFAULT 0,
  swap              numeric NOT NULL DEFAULT 0,
  fee               numeric NOT NULL DEFAULT 0,
  order_id          bigint,
  position_id       bigint,
  deal_time         timestamptz NOT NULL,
  created_at        timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS deals_source_dealid_uk ON deals (source, deal_id);
-- Query patterns: window scans by time, per-login, per-canonical, cash-movement by action.
CREATE INDEX IF NOT EXISTS deals_deal_time_idx        ON deals (deal_time);
CREATE INDEX IF NOT EXISTS deals_login_time_idx        ON deals (login, deal_time);
CREATE INDEX IF NOT EXISTS deals_canonical_time_idx    ON deals (canonical_symbol, deal_time);
CREATE INDEX IF NOT EXISTS deals_source_action_time_idx ON deals (source, action, deal_time);

-- ------------------------------------------------------------------
-- positions  (v2-NEW: v1 held these only in-memory / POST /api/coverage/positions;
-- they were never persisted to Supabase). v2 persists the open-position snapshot so
-- restarts and ad-hoc queries do not depend solely on in-memory PositionManager.
-- Live-populated by the feed writer -- the import does NOT migrate positions (v1 has none).
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS positions (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source             text    NOT NULL DEFAULT 'bbook',
  position_id        bigint,                       -- MT5 position number (feed identity); null for coverage snapshot rows
  login              bigint  NOT NULL,
  symbol             text    NOT NULL DEFAULT '',
  canonical_symbol   text    NOT NULL DEFAULT '',
  direction          text    NOT NULL DEFAULT '',
  volume_lots        numeric NOT NULL DEFAULT 0,
  volume_normalized  numeric NOT NULL DEFAULT 0,
  open_price         numeric NOT NULL DEFAULT 0,
  current_price      numeric NOT NULL DEFAULT 0,
  profit             numeric NOT NULL DEFAULT 0,
  swap               numeric NOT NULL DEFAULT 0,
  open_time          timestamptz,
  updated_at         timestamptz NOT NULL DEFAULT now()
);
-- Identity: (source, position_id) when the feed supplies a position number.
CREATE UNIQUE INDEX IF NOT EXISTS positions_source_posid_uk
  ON positions (source, position_id) WHERE position_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS positions_login_idx     ON positions (login);
CREATE INDEX IF NOT EXISTS positions_canonical_idx ON positions (canonical_symbol);

-- ------------------------------------------------------------------
-- trade_audit_log  (v1: inline, append-only)  no conflict key
-- Records deal-field modifications (price/volume/profit) with old/new values.
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS trade_audit_log (
  id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  source         text    NOT NULL DEFAULT 'bbook',
  deal_id        bigint,
  position_id    bigint,
  login          bigint  NOT NULL,
  symbol         text    NOT NULL DEFAULT '',
  field_changed  text    NOT NULL,
  old_value      text,
  new_value      text,
  changed_by     text    NOT NULL DEFAULT '',
  change_type    text    NOT NULL DEFAULT 'modified',
  detected_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS trade_audit_log_detected_idx ON trade_audit_log (detected_at DESC);
CREATE INDEX IF NOT EXISTS trade_audit_log_login_idx    ON trade_audit_log (login);
CREATE INDEX IF NOT EXISTS trade_audit_log_symbol_idx   ON trade_audit_log (symbol);
