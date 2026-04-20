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
    if (!isFinite(price)) return '—';
    const d = digitsFor(symbol, price);
    return price.toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
  }

  return { digitsFor, fmtPrice };
}
