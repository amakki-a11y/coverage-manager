-- 0090_functions.sql
-- Local SQL re-expression of v1's Supabase RPCs (CLAUDE.md Supabase functions).
-- Run locally these are sub-millisecond; the "avoid the 46s/81s roundtrip" reason
-- for the originals does not exist here, but keeping them as set-based functions
-- preserves the API surface the controllers call.

-- Canonical-key normalization: strip a trailing .c/.m, strip trailing '-', uppercase.
-- Mirrors the normalization the v1 aggregate_* functions applied.
CREATE OR REPLACE FUNCTION cm_canonical_key(sym text)
RETURNS text LANGUAGE sql IMMUTABLE AS $$
  SELECT upper(
           regexp_replace(
             regexp_replace(coalesce(sym, ''), '\.(c|m)$', '', 'i'),
             '-+$', ''
           )
         );
$$;

-- Settled B-Book P&L per canonical key over a time window.
--   profit + swap on OUT deals (entry in 1,2,3) + commission + fee on all trade deals.
-- Trade deals only (action < 2 excludes BALANCE/CREDIT). Lebanon-time boundaries are
-- applied by the caller (from_ts/to_ts arrive already in UTC).
CREATE OR REPLACE FUNCTION aggregate_bbook_settled_pnl(
  from_ts          timestamptz,
  to_ts            timestamptz,
  excluded_logins  bigint[] DEFAULT '{}'
)
RETURNS TABLE(canonical_key text, net_pnl numeric)
LANGUAGE sql STABLE AS $$
  SELECT cm_canonical_key(canonical_symbol) AS canonical_key,
         SUM( CASE WHEN entry IN (1,2,3) THEN profit + swap ELSE 0 END
              + commission + fee ) AS net_pnl
  FROM deals
  WHERE source = 'bbook'
    AND action < 2
    AND deal_time >= from_ts
    AND deal_time <  to_ts
    AND NOT (login = ANY (excluded_logins))
  GROUP BY cm_canonical_key(canonical_symbol);
$$;

-- Full per-symbol aggregation for /api/exposure/pnl (Exposure closed row, P&L tab,
-- Compare Full Table). Volume includes IN + OUT trade deals; profit/swap are OUT-only;
-- commission/fee from all trade deals; buy/sell volume split by action (0=BUY,1=SELL).
CREATE OR REPLACE FUNCTION aggregate_bbook_pnl_full(
  from_ts          timestamptz,
  to_ts            timestamptz,
  excluded_logins  bigint[] DEFAULT '{}'
)
RETURNS TABLE(
  symbol            text,
  deal_count        bigint,
  total_profit      numeric,
  total_commission  numeric,
  total_swap        numeric,
  total_fee         numeric,
  total_volume      numeric,
  buy_volume        numeric,
  sell_volume       numeric
)
LANGUAGE sql STABLE AS $$
  SELECT cm_canonical_key(canonical_symbol) AS symbol,
         count(*)                                                        AS deal_count,
         SUM(CASE WHEN entry IN (1,2,3) THEN profit ELSE 0 END)          AS total_profit,
         SUM(commission)                                                 AS total_commission,
         SUM(CASE WHEN entry IN (1,2,3) THEN swap ELSE 0 END)            AS total_swap,
         SUM(fee)                                                        AS total_fee,
         SUM(volume)                                                     AS total_volume,
         SUM(CASE WHEN action = 0 THEN volume ELSE 0 END)                AS buy_volume,
         SUM(CASE WHEN action = 1 THEN volume ELSE 0 END)                AS sell_volume
  FROM deals
  WHERE source = 'bbook'
    AND action < 2
    AND deal_time >= from_ts
    AND deal_time <  to_ts
    AND NOT (login = ANY (excluded_logins))
  GROUP BY cm_canonical_key(canonical_symbol);
$$;

-- Latest exposure snapshot per canonical symbol at or before an anchor instant.
CREATE OR REPLACE FUNCTION latest_snapshots_before(anchor timestamptz)
RETURNS SETOF exposure_snapshots
LANGUAGE sql STABLE AS $$
  SELECT DISTINCT ON (canonical_symbol) *
  FROM exposure_snapshots
  WHERE snapshot_time <= anchor
  ORDER BY canonical_symbol, snapshot_time DESC;
$$;
