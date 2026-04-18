// Phase 2.5 — Bridge Execution Analysis types. Match the C# shapes in CoverageManager.Core.Models.Bridge.

export type BridgeSide = 'BUY' | 'SELL';

export interface CovFill {
  dealId: string;
  volume: number;
  price: number;
  timeUtc: string; // ISO 8601
  timeDiffMs: number;
  lpName?: string | null;
  mtTicket?: number | null;
  mtDealId?: number | null;
  makerOrderId?: string | null;
}

export interface ExecutionPair {
  clientDealId: string;
  cenOrdId: string;
  clientMtTicket?: number | null;
  clientMtDealId?: number | null;
  clientMtLogin?: number | null;
  symbol: string;
  side: BridgeSide;
  clientVolume: number;
  clientPrice: number;
  clientTimeUtc: string;
  covVolume: number;
  covFills: CovFill[];
  coverageRatio: number;
  avgCovPrice: number;
  priceEdge: number;
  pips: number;
  maxTimeDiffMs: number;
  minTimeDiffMs: number;
  createdAtUtc: string;
}

export interface BridgeExecutionsResponse {
  from: string;
  to: string;
  symbol: string | null;
  count: number;
  pairs: ExecutionPair[];
}

export interface BridgeHealth {
  mode: string;
  state: 'Disconnected' | 'Connecting' | 'LoggedIn' | 'Error' | 'Stubbed';
  lastMessageUtc: string | null;
  messagesReceived: number;
  lastError: string | null;
  pairsInMemory: number;
}

export type BridgeSocketMessage =
  | { type: 'pair'; pair: ExecutionPair };
