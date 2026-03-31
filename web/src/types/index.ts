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

export interface ExposureMessage {
  type: 'exposure_update';
  data: {
    exposure: ExposureSummary[];
    prices: PriceQuote[];
    timestamp: string;
  };
}
