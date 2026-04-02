export interface SymbolExposure {
  symbol: string;
  clientBuyVolume: number;
  clientSellVolume: number;
  clientNetVolume: number;
  clientPnl: number;
  clientAvgEntryPrice: number;
  clientAvgExitPrice: number;
  clientTradeCount: number;
  clientWins: number;

  coverageBuyVolume: number;
  coverageSellVolume: number;
  coverageNetVolume: number;
  coveragePnl: number;
  coverageAvgEntryPrice: number;
  coverageAvgExitPrice: number;
  coverageTradeCount: number;
  coverageWins: number;

  netExposure: number;
  hedgePercent: number;
  entryPriceDelta: number;
  exitPriceDelta: number;
  netPnl: number;
  lastUpdated: string;
}

export interface TradeRecord {
  symbol: string;
  side: 'client' | 'coverage';
  direction: 'BUY' | 'SELL';
  volume: number;
  entryPrice: number;
  exitPrice: number;
  entryTime: string;
  exitTime: string;
  profit: number;
}

export interface CompareMessage {
  type: 'exposure_update';
  timestamp: string;
  symbols: SymbolExposure[];
}
