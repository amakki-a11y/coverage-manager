// Client-side mirror of BridgePipResolver. Used for formatting Pips in the table when the
// backend value is NaN / missing (rare — e.g., when the row is still accumulating coverage).

export function getPipSize(symbol: string, samplePrice: number): number {
  if (!symbol) return heuristic(samplePrice);
  const upper = symbol.toUpperCase();
  if (upper.includes('XAU')) return 0.1;
  if (upper.includes('XAG')) return 0.001;
  if (upper.endsWith('JPY')) return 0.01;
  if (isStandardFxPair(upper)) return 0.0001;
  return heuristic(samplePrice);
}

function isStandardFxPair(upper: string): boolean {
  if (upper.length !== 6) return false;
  for (let i = 0; i < 6; i++) {
    const c = upper.charCodeAt(i);
    if (c < 65 || c > 90) return false;
  }
  return true;
}

function heuristic(price: number): number {
  const abs = Math.abs(price);
  if (abs >= 1000) return 0.1;
  if (abs >= 10) return 0.01;
  if (abs >= 0.1) return 0.0001;
  return 0.00001;
}
