export interface Position {
  source: 'bbook' | 'coverage';
  login: number;
  symbol: string;
  canonicalSymbol: string;
  direction: 'BUY' | 'SELL';
  volumeLots: number;
  volumeNormalized: number;
  openPrice: number;
  currentPrice: number;
  profit: number;
  swap: number;
  openTime: string;
}

export interface ExposureSummary {
  canonicalSymbol: string;
  bBookBuyVolume: number;
  bBookBuyAvgPrice: number;
  bBookSellVolume: number;
  bBookSellAvgPrice: number;
  bBookNetVolume: number;
  bBookPnL: number;
  coverageBuyVolume: number;
  coverageBuyAvgPrice: number;
  coverageSellVolume: number;
  coverageSellAvgPrice: number;
  coverageNetVolume: number;
  coveragePnL: number;
  netVolume: number;
  netPnL: number;
  hedgeRatio: number;
}

export interface SymbolMapping {
  id: string;
  canonical_name: string;
  bbook_symbol: string;
  bbook_contract_size: number;
  coverage_symbol: string;
  coverage_contract_size: number;
  digits: number;
  profit_currency: string;
  is_active: boolean;
}

export interface PriceQuote {
  symbol: string;
  bid: number;
  ask: number;
  spread: number;
  timestamp: string;
}

export interface AccountSettings {
  id: string;
  account_type: 'manager' | 'coverage';
  label: string;
  server: string;
  login: number;
  password: string;
  group_mask: string;
  is_active: boolean;
  created_at: string;
  updated_at: string;
}

export interface AlertEvent {
  id: string;
  threshold_id: string;
  trigger_type: string;
  symbol: string;
  severity: 'info' | 'warning' | 'critical';
  message: string;
  threshold_value: number;
  actual_value: number;
  triggered_at: string;
  acknowledged: boolean;
  acknowledged_at?: string;
}

export interface ExposureMessage {
  type: 'exposure_update';
  data: {
    exposure: ExposureSummary[];
    prices: PriceQuote[];
    alerts?: AlertEvent[];
    alertCount: number;
    timestamp: string;
  };
}

/**
 * Per-canonical-symbol floating P&L decomposition. Backend ships this on every
 * price_update frame (Phase 2.17) so the frontend can overlay live floating
 * onto ExposureSummary at full tick cadence without recomputing the heavy
 * full-state aggregation. Sum already includes Swap to match what
 * CalculateExposure puts in ExposureSummary.bBookPnL / coveragePnL.
 */
export interface FloatingPnL {
  canonicalSymbol: string;
  bBook: number;
  coverage: number;
}

/**
 * Lightweight price-only WS frame, pushed at higher cadence (~20 Hz) than the
 * full exposure_update frame (~10 Hz). The backend sends this on every MT5
 * tick — bypasses the heavy per-position exposure recompute so the bid price
 * shown under each symbol stays fresh even when the position book is large.
 *
 * Phase 2.17 added `floatingPnls` so the frontend can also keep B-Book /
 * Coverage / Net P&L cells fresh at the same cadence (Exposure open row,
 * Net P&L tab "Current", Topbar "Net P&L Today" tile).
 */
export interface PriceUpdateMessage {
  type: 'price_update';
  data: {
    prices: PriceQuote[];
    floatingPnls?: FloatingPnL[];
    timestamp: string;
  };
}

export type WsMessage = ExposureMessage | PriceUpdateMessage;

// ---- Period P&L (Net P&L tab) ----

export interface PeriodPnLSide {
  beginFloating: number;
  currentFloating: number;
  floatingDelta: number;
  settled: number;
  net: number;
  beginFromSnapshot: boolean;
  hasOpenPosition: boolean;
}

export interface PeriodPnLEdge {
  floating: number;
  settled: number;
  net: number;
}

export interface PeriodPnLRow {
  canonicalSymbol: string;
  bBook: PeriodPnLSide;
  coverage: PeriodPnLSide;
  edge: PeriodPnLEdge;
}

export interface PeriodPnLResponse {
  from: string;
  to: string;
  beginAnchorUtc: string;
  rows: PeriodPnLRow[];
  totals: PeriodPnLRow;
}

// ---- Snapshot schedules ----

export type SnapshotCadence = 'daily' | 'weekly' | 'monthly' | 'custom';

export interface SnapshotSchedule {
  id: string;
  name: string;
  cadence: SnapshotCadence;
  cron_expr?: string | null;
  tz: string;
  enabled: boolean;
  last_run_at?: string | null;
  next_run_at?: string | null;
  created_at?: string;
  updated_at?: string;
}

export interface ExposureSnapshot {
  id: string;
  canonical_symbol: string;
  snapshot_time: string;
  bbook_pnl: number;
  coverage_pnl: number;
  net_pnl: number;
  trigger_type: string;
  label: string;
}

// ---- Reconciliation ----

export interface ReconciliationRun {
  id: string;
  trigger_type: 'scheduled' | 'manual';
  window_from: string;
  window_to: string;
  started_at: string;
  finished_at?: string | null;
  mt5_deal_count: number;
  supabase_deal_count: number;
  backfilled: number;
  ghost_deleted: number;
  modified: number;
  error?: string | null;
  notes: string;
}

// =========================================================================
// Equity P&L tab
// =========================================================================

export interface EquityPnLRow {
  login: number;
  source: 'bbook' | 'coverage';
  name: string;
  group: string;
  beginEquity: number;
  netDepositWithdraw: number;
  netCredit: number;
  commRebate: number;
  spreadRebate: number;
  adjustment: number;
  profitShare: number;
  supposedEquity: number;
  currentEquity: number;
  pl: number;
  netPl: number;
  beginFromSnapshot: boolean;
  currentIsLive: boolean;
}

export interface EquityPnLResponse {
  from: string;              // YYYY-MM-DD (Beirut)
  to: string;
  beginAnchorUtc: string;
  endAnchorUtc: string;
  clientRows: EquityPnLRow[];
  coverageRows: EquityPnLRow[];
  clientsTotal: EquityPnLRow | null;
  coverageTotal: EquityPnLRow | null;
  brokerEdge: number;
}

export interface EquityPnLClientConfig {
  login: number;
  source: 'bbook' | 'coverage';
  comm_rebate_pct: number;     // 0..100
  ps_pct: number;              // 0..100
  ps_contract_start?: string | null;     // ISO date
  ps_cum_pl: number;
  ps_low_water_mark: number;
  ps_last_processed_month?: string | null;
  notes?: string | null;
  updated_at?: string | null;
}

export interface SpreadRebateRate {
  login: number;
  source: 'bbook' | 'coverage';
  canonical_symbol: string;
  rate_per_lot: number;
  updated_at?: string | null;
}

// Phase 2 — login groups for per-group rebate/PS config at scale.
export interface LoginGroup {
  id?: string;
  name: string;
  description?: string | null;
  created_at?: string | null;
  updated_at?: string | null;
}

export interface LoginGroupMember {
  group_id: string;
  login: number;
  source: 'bbook' | 'coverage';
  priority: number;
  added_at?: string | null;
}

export interface EquityPnLGroupConfig {
  group_id: string;
  comm_rebate_pct: number;
  ps_pct: number;
  notes?: string | null;
  updated_at?: string | null;
}

export interface GroupSpreadRebateRate {
  group_id: string;
  canonical_symbol: string;
  rate_per_lot: number;
  updated_at?: string | null;
}
