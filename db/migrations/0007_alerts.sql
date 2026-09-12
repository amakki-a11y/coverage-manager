-- 0007_alerts.sql
-- Risk-alert configuration + fired events. The live v1 table backing RiskThreshold
-- is named `alert_rules` (CLAUDE.md's "risk_thresholds" is the concept name, not the
-- table the code hits). alert_events references alert_rules.

-- ------------------------------------------------------------------
-- alert_rules  (v1: inline)  PK id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alert_rules (
  id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  symbol       text    NOT NULL DEFAULT '',       -- '' => all symbols
  trigger_type text    NOT NULL,                  -- exposure | hedge_ratio | pnl | account_exposure
  operator     text    NOT NULL,                  -- gt | lt | gte | lte
  value        numeric NOT NULL,
  severity     text    NOT NULL DEFAULT 'warning',-- critical | warning | info
  enabled      boolean NOT NULL DEFAULT true,
  created_at   timestamptz DEFAULT now(),
  updated_at   timestamptz DEFAULT now()
);
DROP TRIGGER IF EXISTS trg_alert_rules_touch ON alert_rules;
CREATE TRIGGER trg_alert_rules_touch
  BEFORE UPDATE ON alert_rules
  FOR EACH ROW EXECUTE FUNCTION touch_updated_at();

-- ------------------------------------------------------------------
-- alert_events  (v1: inline, append-only)  PK id
-- ------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS alert_events (
  id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  threshold_id     uuid NOT NULL REFERENCES alert_rules(id) ON DELETE CASCADE,
  trigger_type     text NOT NULL,
  symbol           text NOT NULL DEFAULT '',
  severity         text NOT NULL DEFAULT 'warning',
  message          text NOT NULL DEFAULT '',
  threshold_value  numeric NOT NULL,
  actual_value     numeric NOT NULL,
  triggered_at     timestamptz NOT NULL DEFAULT now(),
  acknowledged     boolean NOT NULL DEFAULT false,
  acknowledged_at  timestamptz
);
CREATE INDEX IF NOT EXISTS alert_events_triggered_idx ON alert_events (triggered_at DESC);
CREATE INDEX IF NOT EXISTS alert_events_unack_idx      ON alert_events (acknowledged) WHERE acknowledged = false;
