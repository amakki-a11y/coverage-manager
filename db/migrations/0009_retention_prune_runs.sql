-- 0009_retention_prune_runs.sql
-- Audit log for the nightly 12-month deal-retention pruner (V2_PLAN 5.5; owner decision
-- 2026-09-12). One row per run, including refused and failed runs, so "what did the pruner
-- delete, and when" is answerable from the database rather than only from log files.
--
-- The pruner deletes ONLY deals with deal_time < cutoff_utc, where
--   cutoff_utc = (date_trunc('day', (now() AT TIME ZONE 'UTC')) - interval '<months> months') AT TIME ZONE 'UTC'
-- (months subtracted before the zone is re-attached, so the session timezone cannot shift it)
-- It refuses (deletes nothing, records error) if configured below the decided 12 months.

CREATE TABLE IF NOT EXISTS retention_prune_runs (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  trigger_type      text        NOT NULL DEFAULT 'scheduled' CHECK (trigger_type IN ('scheduled','manual')),
  retention_months  integer     NOT NULL,
  cutoff_utc        timestamptz NOT NULL,
  started_at        timestamptz NOT NULL DEFAULT now(),
  finished_at       timestamptz,
  deleted           bigint      NOT NULL DEFAULT 0,
  batches           integer     NOT NULL DEFAULT 0,
  error             text,
  notes             text        NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS retention_prune_runs_started_idx ON retention_prune_runs (started_at DESC);
