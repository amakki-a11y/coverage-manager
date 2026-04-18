-- Phase 2.5 — Bridge Execution Analysis
-- Stores CLIENT fills paired with their COV_OUT legs from the Centroid Dropcopy feed.
-- Run this in the Supabase SQL editor (project: svhmhcqopkdgccnzgvzp).

create table if not exists bridge_executions (
    id                  uuid primary key default gen_random_uuid(),
    client_deal_id      text not null unique,     -- FIX tag 17 (ExecID) of the CLIENT fill
    cen_ord_id          text not null,            -- FIX tag 37 (OrderID)
    symbol              text not null,            -- canonical symbol (post mapping)
    side                text not null check (side in ('BUY', 'SELL')),

    client_volume       numeric not null,
    client_price        numeric not null,
    client_time         timestamptz not null,

    cov_volume          numeric not null default 0,
    cov_fills           jsonb   not null default '[]'::jsonb,
    -- cov_fills shape: [{dealId, volume, price, time, timeDiffMs, lpName?}]

    coverage_ratio      numeric generated always as (
        case when client_volume > 0 then cov_volume / client_volume else 0 end
    ) stored,

    avg_cov_price       numeric,
    price_edge          numeric,
    pips                numeric,
    max_time_diff_ms    int,
    min_time_diff_ms    int,

    created_at          timestamptz not null default now(),
    updated_at          timestamptz not null default now()
);

create index if not exists bridge_executions_symbol_time_idx
    on bridge_executions (symbol, client_time desc);

create index if not exists bridge_executions_time_idx
    on bridge_executions (client_time desc);

create index if not exists bridge_executions_cen_ord_id_idx
    on bridge_executions (cen_ord_id);

-- Keep updated_at fresh whenever a row is modified.
create or replace function bridge_executions_touch_updated_at()
returns trigger language plpgsql as $$
begin
    new.updated_at := now();
    return new;
end;
$$;

drop trigger if exists bridge_executions_touch on bridge_executions;
create trigger bridge_executions_touch
    before update on bridge_executions
    for each row execute function bridge_executions_touch_updated_at();

-- Optional: per-symbol pip_size override used by BridgePipResolver.
-- Keeps per-broker instrument specs without redeploying C# code.
alter table symbol_mappings
    add column if not exists pip_size numeric;

comment on column symbol_mappings.pip_size is
    'Pip size used by the Bridge tab pip-conversion. NULL => resolver falls back to symbol-name + price heuristics.';
