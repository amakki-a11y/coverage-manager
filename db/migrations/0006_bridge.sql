-- 0006_bridge.sql
-- Centroid Bridge (coverage execution dropcopy) -- unrelated to the Live Bridge feed.
-- bridge_settings (singleton creds/mode) + bridge_executions (paired CLIENT<->COV_OUT).
-- bridge_executions DDL reproduces v1's 20260416 migration (authoritative).

-- ------------------------------------------------------------------
-- bridge_settings  (v1: inline, singleton)  PK id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bridge_settings (
  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  enabled     boolean NOT NULL DEFAULT false,
  mode        text    NOT NULL DEFAULT 'Stub',   -- Stub | Live | Replay
  base_url    text    NOT NULL DEFAULT 'https://bridge.centroidsol.com',
  client_code text    NOT NULL DEFAULT '',
  username    text    NOT NULL DEFAULT '',
  password    text    NOT NULL DEFAULT '',
  notes       text    NOT NULL DEFAULT '',
  updated_at  timestamptz NOT NULL DEFAULT now()
);
DROP TRIGGER IF EXISTS trg_bridge_settings_touch ON bridge_settings;
CREATE TRIGGER trg_bridge_settings_touch
  BEFORE UPDATE ON bridge_settings
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- ------------------------------------------------------------------
-- bridge_executions  (v1: 20260416)  unique client_deal_id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bridge_executions (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  client_deal_id     text NOT NULL UNIQUE,       -- FIX tag 17 (ExecID)
  cen_ord_id         text NOT NULL,              -- FIX tag 37 (OrderID)
  symbol             text NOT NULL,              -- canonical
  side               text NOT NULL CHECK (side IN ('BUY','SELL')),

  client_volume      numeric NOT NULL,
  client_price       numeric NOT NULL,
  client_time        timestamptz NOT NULL,
  client_mt_login    bigint,
  client_mt_ticket   bigint,
  client_mt_deal_id  bigint,

  cov_volume         numeric NOT NULL DEFAULT 0,
  cov_fills          jsonb   NOT NULL DEFAULT '[]'::jsonb,
  coverage_ratio     numeric GENERATED ALWAYS AS (
                        CASE WHEN client_volume > 0 THEN cov_volume / client_volume ELSE 0 END
                      ) STORED,

  avg_cov_price      numeric,
  price_edge         numeric,
  pips               numeric,
  max_time_diff_ms   integer,
  min_time_diff_ms   integer,

  created_at         timestamptz NOT NULL DEFAULT now(),
  updated_at         timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS bridge_executions_symbol_time_idx ON bridge_executions (symbol, client_time DESC);
CREATE INDEX IF NOT EXISTS bridge_executions_time_idx        ON bridge_executions (client_time DESC);
CREATE INDEX IF NOT EXISTS bridge_executions_cen_ord_id_idx  ON bridge_executions (cen_ord_id);

DROP TRIGGER IF EXISTS bridge_executions_touch ON bridge_executions;
CREATE TRIGGER bridge_executions_touch
  BEFORE UPDATE ON bridge_executions
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();
