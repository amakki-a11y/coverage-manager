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
  bbookBuyVolume: number;
  bbookBuyAvgPrice: number;
  bbookSellVolume: number;
  bbookSellAvgPrice: number;
  bbookNetVolume: number;
  bbookPnL: number;
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
  canonicalName: string;
  bbookSymbol: string;
  bbookContractSize: number;
  coverageSymbol: string;
  coverageContractSize: number;
  digits: number;
  profitCurrency: string;
  isActive: boolean;
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
