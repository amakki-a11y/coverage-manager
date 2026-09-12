@{
  # Import manifest for v1 Supabase -> v2 local Postgres.
  #
  #   Mode 'config-exact'  : dealer-entered / config data. Re-runnable via upsert
  #                          (ON CONFLICT DO UPDATE). Verify requires an EXACT
  #                          source==target row-count match.
  #   Mode 'archive-upsert': high-volume history (deals, executions, audit).
  #                          Insert-missing only (ON CONFLICT DO NOTHING) so a
  #                          re-run and the live feed cannot clobber rows. Verify
  #                          requires target >= source (>= because the live feed may
  #                          have added rows since the snapshot).
  #
  #   Order  : load order. Parents before children (FK-safe).
  #   Keys   : conflict-target columns (must match a unique index / PK on the table).
  #   Checks : verify probes. 'count' always implied. 'sum:<col>' compares a numeric
  #            aggregate to the penny. 'checksum' compares an ordered md5 of the whole
  #            table (config-exact only) for byte-level fidelity.
  #
  # NOTE: `positions` is deliberately ABSENT -- v1 never persisted positions to
  # Supabase (in-memory only), so there is nothing to import; v2 live-populates it.
  # `account_settings`, `reconciliation_runs`, and the unused stubs are dropped in v2.

  # ---------------------------------------------------------------------------
  # RETENTION (decided 2026-09-12, V2_PLAN section 5.5)
  #
  #   The v2 local Postgres keeps a ROLLING 12 MONTHS of closed deals. The Exposure
  #   tab's "closed trades" section looks back at most 12 months, so local storage is
  #   sized to the dealer-facing look-back -- not to the full history of the books.
  #
  #   This ONE setting drives BOTH enforcement points so they cannot drift:
  #     * import scope  -- tables declaring a WindowColumn are filtered to
  #                        "<WindowColumn> >= <cutoff>" when extracted from v1, and
  #                        verify.ps1 applies the SAME predicate to BOTH sides so
  #                        counts/sums stay apples-to-apples.
  #     * prune policy  -- the same cutoff expression removes aged-out rows locally
  #                        (see db/README.md "Retention"; the automated pruner is a
  #                        Phase 2 runtime job and is NOT built yet).
  #
  #   Cutoff is UTC-day-stable: date_trunc('day', now() UTC) - RetentionMonths.
  #   Set RetentionMonths = 0 (or pass -RetentionMonths 0) to disable windowing.
  #
  #   Deeper look-back than the window will be served by a future ON-DEMAND,
  #   READ-ONLY request to the accounting system through a narrow, purpose-built API
  #   -- never a direct DB key into the books. That fetch is DEFERRED; do not build it.
  #
  #   Covers closed `deals` only. trade_audit_log / bridge_executions / alert_events
  #   are NOT covered by this decision (V2_PLAN section 9.7) -- they import in full
  #   and are never pruned until a separate call is made.
  # ---------------------------------------------------------------------------
  RetentionMonths = 12

  Tables = @(
    @{ Name='symbol_mappings';                 Order=10;  Mode='config-exact';   Keys=@('id'); Checks=@('count','checksum') }
    @{ Name='trading_accounts';                Order=20;  Mode='config-exact';   Keys=@('source','login'); Checks=@('count','sum:balance','sum:equity') }
    @{ Name='moved_accounts';                  Order=30;  Mode='config-exact';   Keys=@('login'); Checks=@('count') }

    @{ Name='exposure_snapshots';              Order=40;  Mode='config-exact';   Keys=@('canonical_symbol','snapshot_time'); Checks=@('count','sum:net_pnl') }
    @{ Name='snapshot_schedules';              Order=45;  Mode='config-exact';   Keys=@('id'); Checks=@('count') }

    @{ Name='account_equity_snapshots';        Order=50;  Mode='config-exact';   Keys=@('login','source','snapshot_time'); Checks=@('count','sum:equity') }
    @{ Name='equity_pnl_client_config';        Order=55;  Mode='config-exact';   Keys=@('login','source'); Checks=@('count','sum:comm_rebate_pct') }
    @{ Name='equity_pnl_spread_rebates';       Order=56;  Mode='config-exact';   Keys=@('login','source','canonical_symbol'); Checks=@('count','sum:rate_per_lot') }

    @{ Name='login_groups';                    Order=60;  Mode='config-exact';   Keys=@('id'); Checks=@('count') }
    @{ Name='login_group_members';             Order=61;  Mode='config-exact';   Keys=@('group_id','login','source'); Checks=@('count') }
    @{ Name='equity_pnl_group_config';         Order=62;  Mode='config-exact';   Keys=@('group_id'); Checks=@('count') }
    @{ Name='equity_pnl_group_spread_rebates'; Order=63;  Mode='config-exact';   Keys=@('group_id','canonical_symbol'); Checks=@('count') }

    @{ Name='bridge_settings';                 Order=70;  Mode='config-exact';   Keys=@('id'); Checks=@('count') }
    @{ Name='alert_rules';                     Order=75;  Mode='config-exact';   Keys=@('id'); Checks=@('count') }
    @{ Name='alert_events';                    Order=76;  Mode='config-exact';   Keys=@('id'); Checks=@('count') }

    # History / archive (large; insert-missing only)
    # WindowColumn => subject to the RetentionMonths rolling window (import + verify).
    # ChunkColumn/ChunkSpan/PaceMs => the READ side is walked in key ranges with a pause
    # between them, so the ~2M-row export never issues one giant SELECT against the LIVE
    # v1 database. Idempotent (ON CONFLICT DO NOTHING): an interrupted run resumes by
    # simply re-running. Tune with -ChunkSpan / -PaceMs on the command line.
    @{ Name='deals';                           Order=80;  Mode='archive-upsert'; Keys=@('source','deal_id'); WindowColumn='deal_time'; ChunkColumn='deal_id'; ChunkSpan=50000; PaceMs=200; Checks=@('count','sum:profit','sum:commission','sum:swap','sum:fee') }
    @{ Name='trade_audit_log';                 Order=85;  Mode='archive-upsert'; Keys=@('id'); Checks=@('count') }
    @{ Name='bridge_executions';               Order=90;  Mode='archive-upsert'; Keys=@('client_deal_id'); Checks=@('count','sum:cov_volume') }
  )
}
