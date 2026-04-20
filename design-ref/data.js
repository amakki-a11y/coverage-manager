// Mock FX data for Coverage Manager demo
// Realistic symbol list, prices, and per-symbol exposure the demo ticks.

window.SYMBOLS = [
  { sym: 'XAUUSD',  asset: 'Gold',     cls: 'metal',  bid: 2389.42, digits: 2,  vol: 0.35 },
  { sym: 'XAGUSD',  asset: 'Silver',   cls: 'metal',  bid:  28.731, digits: 3,  vol: 0.45 },
  { sym: 'EURUSD',  asset: 'Euro',     cls: 'fx',     bid: 1.08431, digits: 5,  vol: 0.05 },
  { sym: 'GBPJPY',  asset: 'Pound/Yen',cls: 'fx',     bid: 198.412, digits: 3,  vol: 0.18 },
  { sym: 'USDJPY',  asset: 'Dollar/Yen', cls: 'fx',   bid: 154.217, digits: 3,  vol: 0.10 },
  { sym: 'US30',    asset: 'Dow Jones', cls: 'idx',   bid: 38421.5, digits: 1,  vol: 1.50 },
  { sym: 'NAS100',  asset: 'Nasdaq',   cls: 'idx',    bid: 17892.4, digits: 1,  vol: 2.50 },
  { sym: 'SPX500',  asset: 'S&P 500',  cls: 'idx',    bid: 5218.73, digits: 2,  vol: 0.80 },
  { sym: 'BTCUSD',  asset: 'Bitcoin',  cls: 'crypto', bid: 67412.8, digits: 1,  vol: 12.0 },
  { sym: 'ETHUSD',  asset: 'Ether',    cls: 'crypto', bid: 3318.42, digits: 2,  vol: 1.80 },
  { sym: 'USOIL',   asset: 'WTI Crude',cls: 'comm',   bid: 82.418,  digits: 3,  vol: 0.14 },
  { sym: 'AUDUSD',  asset: 'Aussie',   cls: 'fx',     bid: 0.65182, digits: 5,  vol: 0.04 },
];

// Per-symbol exposure (lots).
// bbBuy / bbSell: client positions. covBuy / covSell: our LP hedge positions.
// To Cover = bbNet - covNet   (negative = we need to SELL more; positive = BUY more)
window.EXPOSURE = {
  XAUUSD:  { bbBuy: 141.20, bbSell:  42.50, covBuy:   0.00, covSell:  90.00, bbPnL:  -4820.40, covPnL:  +3210.60 },
  XAGUSD:  { bbBuy:  28.50, bbSell:  12.00, covBuy:   4.00, covSell:  18.00, bbPnL:    +218.30, covPnL:    -92.70 },
  EURUSD:  { bbBuy: 185.00, bbSell: 210.50, covBuy:  28.00, covSell:   0.00, bbPnL:   +1045.20, covPnL:   -182.40 },
  GBPJPY:  { bbBuy:  32.10, bbSell:  18.40, covBuy:   0.00, covSell:  12.00, bbPnL:    -418.90, covPnL:   +342.70 },
  USDJPY:  { bbBuy:  62.00, bbSell:  88.50, covBuy:  30.00, covSell:   4.00, bbPnL:    +218.60, covPnL:    +48.20 },
  US30:    { bbBuy:  12.20, bbSell:   4.50, covBuy:   0.00, covSell:   7.50, bbPnL:   -1920.10, covPnL:  +1742.30 },
  NAS100:  { bbBuy:   6.30, bbSell:   3.00, covBuy:   0.00, covSell:   3.00, bbPnL:    -620.40, covPnL:   +510.80 },
  SPX500:  { bbBuy:   8.90, bbSell:   2.40, covBuy:   0.00, covSell:   6.00, bbPnL:    -280.10, covPnL:   +218.30 },
  BTCUSD:  { bbBuy:   3.40, bbSell:   0.80, covBuy:   0.00, covSell:   2.50, bbPnL:   +4120.80, covPnL:  -3980.20 },
  ETHUSD:  { bbBuy:   4.80, bbSell:   1.20, covBuy:   0.00, covSell:   3.50, bbPnL:    +618.20, covPnL:   -542.10 },
  USOIL:   { bbBuy:  18.20, bbSell:  32.50, covBuy:  14.00, covSell:   0.00, bbPnL:    -142.70, covPnL:   +118.40 },
  AUDUSD:  { bbBuy:  45.00, bbSell:  58.00, covBuy:  12.00, covSell:   0.00, bbPnL:    +142.00, covPnL:    -62.40 },
};

// Closed P&L for today (bB and Cov)
window.CLOSED_PNL = {
  XAUUSD:  { bb: +2142.80, cov: -1820.40 },
  XAGUSD:  { bb:  -182.40, cov:  +142.60 },
  EURUSD:  { bb:  +418.60, cov:  -318.20 },
  GBPJPY:  { bb:  -842.10, cov:  +718.40 },
  USDJPY:  { bb:  +418.00, cov:  -382.60 },
  US30:    { bb: +1820.20, cov: -1610.30 },
  NAS100:  { bb:  +618.40, cov:  -518.20 },
  SPX500:  { bb:  -218.40, cov:  +182.10 },
  BTCUSD:  { bb: +3820.60, cov: -3510.40 },
  ETHUSD:  { bb:  +418.20, cov:  -362.10 },
  USOIL:   { bb:  -120.80, cov:   +94.20 },
  AUDUSD:  { bb:   +62.40, cov:   -38.10 },
};

// Seed alerts for the banner / toasts / history
window.ALERTS_SEED = [
  { id: 'a1', severity: 'warn', title: 'Hedge ratio low',    sym: 'EURUSD', desc: 'Hedge ratio dropped to 11% (threshold 25%)', actual: '11%',     threshold: '25%',     time: '2m ago', ack: false },
  { id: 'a2', severity: 'crit', title: 'Exposure limit',     sym: 'BTCUSD', desc: 'Net client exposure 2.60 lots exceeds 2.00 limit', actual: '2.60', threshold: '2.00', time: '8m ago', ack: false },
  { id: 'a3', severity: 'warn', title: 'News event incoming',sym: 'USDJPY', desc: 'High-impact: BoJ rate decision in 18 minutes',      actual: '',     threshold: '',     time: '12m ago', ack: false },
  { id: 'a4', severity: 'info', title: 'Snapshot captured',  sym: '—',      desc: 'Daily exposure snapshot at 00:00 Asia/Beirut',       actual: '',     threshold: '',     time: '3h ago',  ack: true  },
  { id: 'a5', severity: 'warn', title: 'Coverage disconnect',sym: 'XAUUSD', desc: 'LP collector missed 2 polls — retrying',             actual: '',     threshold: '',     time: '4h ago',  ack: true  },
];

// Alert rules
window.ALERT_RULES = [
  { id: 'r1', sym: 'ALL',    kind: 'hedge_ratio',   op: '<',  val: 25,    unit: '%',   sev: 'warn', enabled: true,  ch: ['in-app','email'] },
  { id: 'r2', sym: 'BTCUSD', kind: 'exposure',      op: '>',  val: 2.00,  unit: 'lots',sev: 'crit', enabled: true,  ch: ['in-app','slack'] },
  { id: 'r3', sym: 'XAUUSD', kind: 'exposure',      op: '>',  val: 100,   unit: 'lots',sev: 'crit', enabled: true,  ch: ['in-app','email','telegram'] },
  { id: 'r4', sym: 'ALL',    kind: 'net_pnl',       op: '<',  val: -5000, unit: 'USD', sev: 'crit', enabled: true,  ch: ['in-app','email'] },
  { id: 'r5', sym: 'ALL',    kind: 'news_event',    op: 'before', val: 30, unit: 'min',sev: 'warn', enabled: true,  ch: ['in-app'] },
  { id: 'r6', sym: 'EURUSD', kind: 'single_client', op: '>',  val: 50,    unit: 'lots',sev: 'warn', enabled: false, ch: ['in-app'] },
];

// Bridge executions mock
window.BRIDGE_ROWS = [
  { id: 'b1', time: '14:32:18.421', sym: 'XAUUSD', side: 'BUY',  cliVol: 2.50, cliPrice: 2389.40, cliLogin: 82451, covFills: [
    { vol: 1.50, price: 2389.42, diffMs: 180, lp: 'Centroid-A' },
    { vol: 1.00, price: 2389.44, diffMs: 420, lp: 'Centroid-A' },
  ], edge: +0.08, pips: +8.0 },
  { id: 'b2', time: '14:31:52.118', sym: 'EURUSD', side: 'SELL', cliVol: 5.00, cliPrice: 1.08432, cliLogin: 81922, covFills: [
    { vol: 5.00, price: 1.08430, diffMs: 95, lp: 'Centroid-A' },
  ], edge: +0.00002, pips: +0.2 },
  { id: 'b3', time: '14:31:12.902', sym: 'US30',   side: 'BUY',  cliVol: 0.50, cliPrice: 38422.0, cliLogin: 82104, covFills: [
    { vol: 0.50, price: 38422.8, diffMs: 1842, lp: 'Centroid-B' },
  ], edge: -0.80, pips: -8.0 },
  { id: 'b4', time: '14:30:48.220', sym: 'BTCUSD', side: 'BUY',  cliVol: 0.10, cliPrice: 67410.0, cliLogin: 82451, covFills: [], edge: 0, pips: 0, anomaly: 'no-cov' },
  { id: 'b5', time: '14:30:02.814', sym: 'XAUUSD', side: 'SELL', cliVol: 1.00, cliPrice: 2389.60, cliLogin: 81208, covFills: [
    { vol: 1.00, price: 2389.58, diffMs: 310, lp: 'Centroid-A' },
  ], edge: +0.02, pips: +2.0 },
];

// Snapshot schedules
window.SCHEDULES = [
  { id: 's1', name: 'Daily',   cadence: 'daily',   cron: '0 0 * * *',   tz: 'Asia/Beirut', enabled: true,  last: '2h ago',  next: '21h 58m' },
  { id: 's2', name: 'Weekly',  cadence: 'weekly',  cron: '0 0 * * 1',   tz: 'Asia/Beirut', enabled: true,  last: '4d ago',  next: '2d 21h' },
  { id: 's3', name: 'Monthly', cadence: 'monthly', cron: '0 0 1 * *',   tz: 'Asia/Beirut', enabled: true,  last: '18d ago', next: '12d 21h' },
  { id: 's4', name: 'London open', cadence: 'custom', cron: '0 8 * * 1-5', tz: 'Europe/London', enabled: false, last: '—',   next: '—' },
];

// Symbol mappings
window.MAPPINGS = [
  { id: 'm1', canonical: 'XAUUSD',  bbook: 'XAUUSD-',  bbSize: 100,   covSym: 'GCM6.c',  covSize: 100,  digits: 2, ccy: 'USD', active: true },
  { id: 'm2', canonical: 'XAGUSD',  bbook: 'XAGUSD-',  bbSize: 5000,  covSym: 'SIK6.c',  covSize: 5000, digits: 3, ccy: 'USD', active: true },
  { id: 'm3', canonical: 'EURUSD',  bbook: 'EURUSD-',  bbSize: 100000,covSym: 'EURUSD-', covSize: 100000,digits:5, ccy: 'USD', active: true },
  { id: 'm4', canonical: 'GBPJPY',  bbook: 'GBPJPY-',  bbSize: 100000,covSym: 'GBPJPY-', covSize: 100000,digits:3, ccy: 'JPY', active: true },
  { id: 'm5', canonical: 'USDJPY',  bbook: 'USDJPY-',  bbSize: 100000,covSym: 'USDJPY-', covSize: 100000,digits:3, ccy: 'JPY', active: true },
  { id: 'm6', canonical: 'US30',    bbook: 'US30-',    bbSize: 1,     covSym: 'YMM6.c',  covSize: 1,    digits: 1, ccy: 'USD', active: true },
  { id: 'm7', canonical: 'NAS100',  bbook: 'NAS100-',  bbSize: 1,     covSym: 'NQM6.c',  covSize: 1,    digits: 1, ccy: 'USD', active: true },
  { id: 'm8', canonical: 'SPX500',  bbook: 'SPX500-',  bbSize: 1,     covSym: 'ESM6.c',  covSize: 1,    digits: 2, ccy: 'USD', active: true },
  { id: 'm9', canonical: 'BTCUSD',  bbook: 'BTCUSD-',  bbSize: 1,     covSym: 'BTCUSD',  covSize: 1,    digits: 1, ccy: 'USD', active: true },
  { id: 'm10',canonical: 'ETHUSD',  bbook: 'ETHUSD-',  bbSize: 1,     covSym: 'ETHUSD',  covSize: 1,    digits: 2, ccy: 'USD', active: true },
  { id: 'm11',canonical: 'USOIL',   bbook: 'USOIL-',   bbSize: 1000,  covSym: 'CLN6.c',  covSize: 1000, digits: 3, ccy: 'USD', active: true },
  { id: 'm12',canonical: 'AUDUSD',  bbook: 'AUDUSD-',  bbSize: 100000,covSym: 'AUDUSD-', covSize: 100000,digits:5, ccy: 'USD', active: true },
];

// Positions (raw)
window.POSITIONS = [
  { login: 82451, source: 'bbook', sym: 'XAUUSD-', canonical: 'XAUUSD', dir: 'BUY', vol: 12.50, openPrice: 2388.20, currentPrice: 2389.42, pnl: +1520.40, openTime: '11:42' },
  { login: 82451, source: 'bbook', sym: 'EURUSD-', canonical: 'EURUSD', dir: 'SELL', vol: 25.00, openPrice: 1.08418, currentPrice: 1.08431, pnl:  -325.00, openTime: '11:31' },
  { login: 81208, source: 'bbook', sym: 'BTCUSD-', canonical: 'BTCUSD', dir: 'BUY', vol: 0.80, openPrice: 67120.40, currentPrice: 67412.80, pnl: +2340.20, openTime: '10:14' },
  { login: 82104, source: 'bbook', sym: 'US30-',   canonical: 'US30',   dir: 'SELL', vol:  4.50, openPrice: 38480.1,  currentPrice: 38421.5,  pnl:  +262.00, openTime: '09:52' },
  { login: 81922, source: 'bbook', sym: 'GBPJPY-', canonical: 'GBPJPY', dir: 'BUY', vol: 18.00, openPrice: 198.518, currentPrice: 198.412, pnl:  -418.60, openTime: '12:05' },
  { login: 96900, source: 'coverage', sym: 'GCM6.c', canonical: 'XAUUSD', dir: 'SELL', vol: 90.00, openPrice: 2388.42, currentPrice: 2389.42, pnl: -9000.00, openTime: '11:44' },
  { login: 96900, source: 'coverage', sym: 'EURUSD-', canonical: 'EURUSD', dir: 'BUY', vol: 28.00, openPrice: 1.08445, currentPrice: 1.08431, pnl: -392.00, openTime: '11:32' },
  { login: 96900, source: 'coverage', sym: 'BTCUSD', canonical: 'BTCUSD', dir: 'SELL', vol: 2.50, openPrice: 67220.0, currentPrice: 67412.8, pnl: -482.00, openTime: '10:15' },
  { login: 96900, source: 'coverage', sym: 'YMM6.c', canonical: 'US30', dir: 'BUY', vol:  7.50, openPrice: 38468.0, currentPrice: 38421.5, pnl:  -348.75, openTime: '09:53' },
];

// Recent activity / audit trail
window.ACTIVITY = [
  { t: '14:32:18', kind: 'hedge',  text: 'Hedged XAUUSD 48.70 lots @ 2389.42', user: 'dealer.max' },
  { t: '14:28:02', kind: 'rule',   text: 'Alert rule "Hedge ratio < 25%" fired on EURUSD', user: 'system' },
  { t: '14:12:41', kind: 'login',  text: 'Login 81208 opened BTCUSD BUY 0.80', user: 'MT5' },
  { t: '13:58:09', kind: 'snap',   text: 'Manual snapshot captured', user: 'dealer.max' },
  { t: '13:42:19', kind: 'hedge',  text: 'Hedged US30 7.50 lots @ 38468.0', user: 'dealer.max' },
  { t: '13:21:45', kind: 'rule',   text: 'Rule "BTCUSD exposure > 2 lots" created', user: 'risk.ops' },
];

// Equity P&L — per-login (client side)
window.EQUITY_PNL_CLIENTS = [
  { login: 82451, group: 'VIP-TierA', beginEq: 240500.00, netDepW: +15000.00, netCred:     0.00, commReb: -482.40,  spreadReb: -1204.80, adj:   0.00, ps:      0.00, supposedEq: 255500.00, currentEq: 252340.80, pl:  -3159.20, netPl:  -4846.40 },
  { login: 82452, group: 'VIP-TierA', beginEq: 180200.00, netDepW:   -500.00, netCred:     0.00, commReb: -218.60,  spreadReb:  -680.20, adj:   0.00, ps:   1240.80, supposedEq: 179700.00, currentEq: 184210.40, pl:   4510.40, netPl:   2370.80 },
  { login: 82453, group: 'IB-Lebanon',beginEq:  92400.00, netDepW: +12000.00, netCred: +5000.00, commReb: -140.20,  spreadReb:  -412.30, adj: -50.00, ps:      0.00, supposedEq: 109400.00, currentEq: 106820.50, pl:  -2579.50, netPl:  -3131.00 },
  { login: 82454, group: 'IB-Lebanon',beginEq:  48120.00, netDepW:      0.00, netCred:     0.00, commReb:  -88.40,  spreadReb:  -220.40, adj:   0.00, ps:      0.00, supposedEq:  48120.00, currentEq:  49320.10, pl:   1200.10, netPl:    891.30 },
  { login: 82455, group: 'Retail',    beginEq:  18500.00, netDepW:  -2000.00, netCred:     0.00, commReb:  -12.40,  spreadReb:   -58.70, adj:   0.00, ps:      0.00, supposedEq:  16500.00, currentEq:  15890.20, pl:   -609.80, netPl:   -680.90 },
  { login: 82456, group: 'Retail',    beginEq:  12200.00, netDepW:      0.00, netCred:     0.00, commReb:   -8.90,  spreadReb:   -42.10, adj:   0.00, ps:      0.00, supposedEq:  12200.00, currentEq:  12410.80, pl:    210.80, netPl:    159.80 },
  { login: 82457, group: 'IB-Lebanon',beginEq: 320800.00, netDepW:      0.00, netCred:     0.00, commReb: -642.10,  spreadReb: -1820.40, adj:   0.00, ps:   3240.00, supposedEq: 320800.00, currentEq: 334120.20, pl:  13320.20, netPl:   7617.70 },
  { login: 82458, group: 'VIP-TierA', beginEq: 510000.00, netDepW: +50000.00, netCred:     0.00, commReb:-1204.80,  spreadReb: -3421.70, adj:   0.00, ps:   5820.00, supposedEq: 560000.00, currentEq: 548210.00, pl: -11790.00, netPl: -22237.50 },
  { login: 82459, group: 'Retail',    beginEq:   4200.00, netDepW:      0.00, netCred:     0.00, commReb:   -2.10,  spreadReb:   -12.80, adj:   0.00, ps:      0.00, supposedEq:   4200.00, currentEq:   4180.30, pl:    -19.70, netPl:    -34.60 },
  { login: 82460, group: 'VIP-TierA', beginEq:  88800.00, netDepW: -10000.00, netCred:     0.00, commReb: -124.40,  spreadReb:  -482.20, adj: -25.00, ps:      0.00, supposedEq:  78800.00, currentEq:  80120.80, pl:   1320.80, netPl:    689.20 },
];
// Coverage side — simpler, no rebates (just broker's LP mirror)
window.EQUITY_PNL_COVERAGE = [
  { login: 96900, group: 'Cov-Centroid', beginEq: 1820000.00, netDepW: +100000.00, netCred: 0.00, commReb: 0, spreadReb: 0, adj: 0, ps: 0, supposedEq: 1920000.00, currentEq: 1935420.80, pl: 15420.80, netPl: 15420.80 },
];

// Login groups
window.LOGIN_GROUPS = [
  { id: 'g1', name: 'VIP-TierA',    members: 3, commPct: 0.08, psPct: 15, spreadRate: 6.00, color: 'amber' },
  { id: 'g2', name: 'IB-Lebanon',   members: 3, commPct: 0.05, psPct:  0, spreadRate: 4.00, color: 'blue' },
  { id: 'g3', name: 'Retail',       members: 3, commPct: 0.03, psPct:  0, spreadRate: 3.50, color: 'teal' },
  { id: 'g4', name: 'Cov-Centroid', members: 1, commPct: 0.00, psPct:  0, spreadRate: 0.00, color: 'gray' },
];

// Markup (per canonical symbol — client vs coverage VWAP edge)
window.MARKUP = [
  { sym: 'XAUUSD', cliDeals: 142, covDeals:  92, cliVWAP: 2389.41, covVWAP: 2389.43, pipEdge: +0.2,  mkUsd: +1842.60, cliPnL:  -4820.40, covPnL: +3210.60 },
  { sym: 'EURUSD', cliDeals: 218, covDeals: 184, cliVWAP: 1.08421, covVWAP: 1.08418, pipEdge: -0.3,  mkUsd:  -621.40, cliPnL:   +182.40, covPnL:  -218.40 },
  { sym: 'US30',   cliDeals:  48, covDeals:  22, cliVWAP: 38464.2, covVWAP: 38466.1, pipEdge: +1.9,  mkUsd:  +320.40, cliPnL:  -1240.50, covPnL:  +840.10 },
  { sym: 'BTCUSD', cliDeals:  32, covDeals:  20, cliVWAP: 63420.8, covVWAP: 63432.1, pipEdge: +11.3, mkUsd:  +488.20, cliPnL:   -920.40, covPnL:  +682.40 },
  { sym: 'XAGUSD', cliDeals:  28, covDeals:  14, cliVWAP:  28.729, covVWAP:  28.732, pipEdge: +0.3,  mkUsd:   +48.20, cliPnL:   +218.30, covPnL:   -92.70 },
  { sym: 'GBPUSD', cliDeals:  94, covDeals:  72, cliVWAP: 1.26412, covVWAP: 1.26408, pipEdge: -0.4,  mkUsd:  -182.40, cliPnL:    -48.20, covPnL:   +62.80 },
];

// Connection health (4 dots)
window.CONNECTIONS = {
  mt5:       { status: 'ok',   name: 'MT5 Manager',    latMs:  18, detail: 'B-Book sink live · 40 logins · 0 ghosts last sweep' },
  collector: { status: 'ok',   name: 'Python Collector', latMs: 42, detail: 'Coverage MT5 · 100ms poll · 94,210 deals synced' },
  centroid:  { status: 'warn', name: 'Centroid Bridge',  latMs: 120, detail: 'Stub mode — live creds pending whitelist' },
  supabase:  { status: 'ok',   name: 'Supabase',         latMs:  28, detail: 'eu-central-1 · 280K deals · 0 lag' },
};

// Reconciliation runs
window.RECON_RUNS = [
  { id: 'rc1', time: '02:05', tz: 'UTC', trigger: 'scheduled', window: '14d', mt5: 94210, supa: 94210, backfill: 0, ghost: 0, modified: 0, ok: true },
  { id: 'rc2', time: '21:04', tz: 'Beirut', trigger: 'manual', window: '7d',  mt5:  42180, supa: 42174, backfill: 6, ghost: 0, modified: 2, ok: true },
  { id: 'rc3', time: '02:05', tz: 'UTC', trigger: 'scheduled', window: '14d', mt5: 93420, supa: 93420, backfill: 0, ghost: 0, modified: 1, ok: true },
  { id: 'rc4', time: '02:05', tz: 'UTC', trigger: 'scheduled', window: '14d', mt5: 93120, supa: 93118, backfill: 2, ghost: 0, modified: 0, ok: true },
];

// Net P&L period rows (Begin / Current / FloatΔ / Settled / Net)
window.NET_PNL_PERIOD = [
  { sym: 'XAUUSD', bbBegin:   -820.00, bbCurr:  -4820.40, bbFloatD: -4000.40, bbSettled:  +2142.80, bbNet:  -1857.60, covBegin:   +620.00, covCurr:  +3210.60, covFloatD: +2590.60, covSettled: -1820.40, covNet:   +770.20, beginFromSnap: true },
  { sym: 'EURUSD', bbBegin:   +320.00, bbCurr:   -218.40, bbFloatD:  -538.40, bbSettled:   +182.40, bbNet:   -356.00, covBegin:   -120.00, covCurr:    +42.80, covFloatD:  +162.80, covSettled:   -92.70, covNet:    +70.10, beginFromSnap: true },
  { sym: 'US30',   bbBegin:  -1800.00, bbCurr:  -1240.50, bbFloatD:  +559.50, bbSettled:   -824.20, bbNet:   -264.70, covBegin:  +1120.00, covCurr:   +840.10, covFloatD:  -279.90, covSettled:  +420.80, covNet:   +140.90, beginFromSnap: true },
  { sym: 'BTCUSD', bbBegin:  +1200.00, bbCurr:   -920.40, bbFloatD: -2120.40, bbSettled:  -1820.40, bbNet:  -3940.80, covBegin:   -820.00, covCurr:   +682.40, covFloatD: +1502.40, covSettled: +1240.40, covNet:  +2742.80, beginFromSnap: false },
  { sym: 'XAGUSD', bbBegin:     +20.00, bbCurr:   +218.30, bbFloatD:  +198.30, bbSettled:   +120.40, bbNet:    318.70, covBegin:    -12.00, covCurr:    -92.70, covFloatD:   -80.70, covSettled:   -48.20, covNet:   -128.90, beginFromSnap: true },
  { sym: 'GBPUSD', bbBegin:     +80.00, bbCurr:    -48.20, bbFloatD:  -128.20, bbSettled:    +62.40, bbNet:    -65.80, covBegin:    -20.00, covCurr:    +62.80, covFloatD:   +82.80, covSettled:   -18.20, covNet:    +64.60, beginFromSnap: true },
];

// Positions Compare — per-symbol summary (hedge% + net edges)
window.COMPARE_SUMMARY = [
  { sym: 'XAUUSD', hedge:  91, cliNet:   98.70, covNet:   90.00, netD:   8.70,  cliPL:  -4820.40, covPL:  +3210.60, plD:  +8031.00 },
  { sym: 'EURUSD', hedge:  11, cliNet: +180.50, covNet:   20.00, netD: 160.50,  cliPL:   +182.40, covPL:   -92.70, plD:   -275.10 },
  { sym: 'US30',   hedge:  77, cliNet:  -12.20, covNet:   -9.40, netD:  -2.80,  cliPL:  -1240.50, covPL:   +840.10, plD:  +2080.60 },
  { sym: 'BTCUSD', hedge:   8, cliNet:   +2.60, covNet:    0.20, netD:   2.40,  cliPL:   -920.40, covPL:   +682.40, plD:  +1602.80 },
  { sym: 'XAGUSD', hedge:  88, cliNet:  +16.50, covNet:   14.00, netD:   2.50,  cliPL:   +218.30, covPL:   -92.70, plD:   -311.00 },
  { sym: 'GBPUSD', hedge: 104, cliNet:  -38.20, covNet:  -40.00, netD:  +1.80,  cliPL:    -48.20, covPL:   +62.80, plD:   +111.00 },
  { sym: 'USDJPY', hedge:  62, cliNet:   +8.00, covNet:    5.00, netD:   3.00,  cliPL:    +28.40, covPL:   -12.80, plD:    -41.20 },
  { sym: 'NAS100', hedge:  48, cliNet:   -3.00, covNet:   -1.40, netD:  -1.60,  cliPL:   -612.20, covPL:  +420.10, plD:  +1032.30 },
];

// Snapshot history entries
window.SNAPSHOTS = [
  { id: 'sn1', t: '21:00 Beirut · 2026-04-19', trigger: 'scheduled', label: 'Daily', symbols: 12, user: 'system' },
  { id: 'sn2', t: '15:30 Beirut · 2026-04-19', trigger: 'manual',    label: 'Pre-NFP',  symbols: 12, user: 'dealer.max' },
  { id: 'sn3', t: '21:00 Beirut · 2026-04-18', trigger: 'scheduled', label: 'Daily', symbols: 12, user: 'system' },
  { id: 'sn4', t: '09:00 Beirut · 2026-04-18', trigger: 'manual',    label: 'Portfolio SEED_TOTAL', symbols:  1, user: 'risk.ops' },
  { id: 'sn5', t: '21:00 Beirut · 2026-04-17', trigger: 'scheduled', label: 'Daily', symbols: 12, user: 'system' },
];

// Spread rebate rates (per login per symbol)
window.SPREAD_REBATES = [
  { id: 'sr1', login: 82451, sym: 'XAUUSD', rate: 8.00, scope: 'login' },
  { id: 'sr2', login: 82451, sym: 'EURUSD', rate: 3.50, scope: 'login' },
  { id: 'sr3', group: 'VIP-TierA', sym: 'XAUUSD', rate: 6.00, scope: 'group' },
  { id: 'sr4', group: 'VIP-TierA', sym: 'EURUSD', rate: 2.50, scope: 'group' },
  { id: 'sr5', group: 'IB-Lebanon',sym: 'XAUUSD', rate: 4.00, scope: 'group' },
];
