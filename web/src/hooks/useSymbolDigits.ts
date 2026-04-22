import { useEffect, useState } from 'react';
import type { SymbolMapping } from '../types';
import { API_BASE } from '../config';

/**
 * Per-instrument price precision source of truth.
 *
 * Loads once (cached module-level) and exposes a `digitsFor(symbol)` resolver
 * that accepts any symbol variant (raw MT5 "XAUUSD-", canonical "XAUUSD",
 * coverage "XAUUSD-", case-insensitive). Falls back to a price-magnitude
 * heuristic when the mapping is missing so every cell still renders something
 * sane, but the real answer comes from `symbol_mappings.digits`.
 */
const API = `${API_BASE}/api/mappings`;

type DigitsMap = Record<string, number>;
let cache: DigitsMap | null = null;
const listeners = new Set<(m: DigitsMap) => void>();

function buildMap(mappings: SymbolMapping[]): DigitsMap {
  const m: DigitsMap = {};
  for (const row of mappings) {
    if (!row.is_active) continue;
    const d = Number(row.digits);
    if (!Number.isFinite(d) || d < 0) continue;
    const norm = (s: string) => s?.replace(/[-.]$/, '').toUpperCase();
    // Index under every variant we might look up — canonical, bbook, coverage —
    // and under each stripped+uppercase form. Kept separate from the C#-side
    // NormalizeKey so this stays small + purely UI.
    if (row.canonical_name) m[norm(row.canonical_name)] = d;
    if (row.bbook_symbol)   m[norm(row.bbook_symbol)]   = d;
    if (row.coverage_symbol) m[norm(row.coverage_symbol)] = d;
  }
  return m;
}

async function load() {
  try {
    const res = await fetch(API);
    if (!res.ok) return;
    const rows: SymbolMapping[] = await res.json();
    cache = buildMap(rows);
    listeners.forEach(fn => fn(cache!));
  } catch { /* keep previous cache if fetch fails */ }
}

/** Heuristic fallback: decimal count by typical price magnitude. */
function heuristicDigits(samplePrice: number | undefined): number {
  if (samplePrice == null || !isFinite(samplePrice)) return 2;
  const abs = Math.abs(samplePrice);
  if (abs >= 1000) return 2;     // indices, metals
  if (abs >= 10)   return 3;     // oil, JPY-ish
  if (abs >= 1)    return 4;     // standard FX with big handle
  return 5;                       // sub-1 FX / crypto
}

export function useSymbolDigits() {
  const [map, setMap] = useState<DigitsMap>(() => cache ?? {});

  useEffect(() => {
    if (!cache) load();
    const fn = (m: DigitsMap) => setMap(m);
    listeners.add(fn);
    return () => { listeners.delete(fn); };
  }, []);

  function digitsFor(symbol: string | undefined | null, samplePrice?: number): number {
    if (!symbol) return heuristicDigits(samplePrice);
    const key = symbol.replace(/[-.]$/, '').toUpperCase();
    const hit = map[key];
    return Number.isFinite(hit) ? hit : heuristicDigits(samplePrice);
  }

  function fmtPrice(symbol: string | undefined | null, price: number): string {
    if (!isFinite(price)) return '\u2014';
    const d = digitsFor(symbol, price);
    // Format with full precision, then strip trailing zeros from the fractional
    // part so indices like US30 show "49,371.20" instead of "49,371.20000" and
    // silver shows "77.858" instead of "77.85800". FX pairs that end on a real
    // trailing zero (e.g. 1.17480 → 1.1748) lose the fractional-pip marker,
    // which is fine for a glance-value bid rendered under the symbol name.
    // Always keep at least 2 decimals so "77" doesn't collapse to "77".
    const formatted = price.toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
    return trimTrailingZeros(formatted, 2);
  }

  return { digitsFor, fmtPrice };
}

/**
 * Strip trailing zeros from a locale-formatted decimal string while keeping
 * at least `minDecimals` places. Works on comma-grouped numbers (thousands
 * separators are untouched) — only the fractional part is trimmed.
 *
 *   trimTrailingZeros("49,371.20000", 2) → "49,371.20"
 *   trimTrailingZeros("77.85800",     2) → "77.858"
 *   trimTrailingZeros("1.10000",      2) → "1.10"
 *   trimTrailingZeros("100.00",       2) → "100.00"
 */
function trimTrailingZeros(s: string, minDecimals: number): string {
  const dot = s.indexOf('.');
  if (dot === -1) return s;
  // Walk back from the end stripping '0', but stop so we keep at least minDecimals.
  let end = s.length;
  const minEnd = dot + 1 + minDecimals;
  while (end > minEnd && s[end - 1] === '0') end--;
  // If trimming lands on the decimal point (all zeros), leave the decimal in
  // place with the minimum places — the while loop's `> minEnd` already does
  // this; belt-and-suspenders for a zero-decimal minDecimals call.
  if (end === dot + 1) end--; // drop the dot itself
  return s.slice(0, end);
}
