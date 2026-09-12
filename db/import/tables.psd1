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
    @{ Name='deals';                           Order=80;  Mode='archive-upsert'; Keys=@('source','deal_id'); Checks=@('count','sum:profit','sum:commission','sum:swap','sum:fee') }
    @{ Name='trade_audit_log';                 Order=85;  Mode='archive-upsert'; Keys=@('id'); Checks=@('count') }
    @{ Name='bridge_executions';               Order=90;  Mode='archive-upsert'; Keys=@('client_deal_id'); Checks=@('count','sum:cov_volume') }
  )
}
