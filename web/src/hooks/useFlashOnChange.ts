import { useEffect, useRef, useState } from 'react';

/**
 * Returns a `flashColor` — a CSS colour the caller can slot into a cell's
 * background or outline briefly after `value` changes. Used to draw the dealer's
 * eye to cells that just updated (price ticks, P&L moves).
 *
 * - Direction-aware: up moves flash green, down moves flash red. Caller can
 *   also pass fixed colours by providing `upColor` / `downColor`.
 * - Debounced by 800 ms so rapid tick streams don't strobe.
 * - Returns `undefined` when no flash is active, so callers can spread it
 *   into `style` safely.
 */
export function useFlashOnChange(
  value: number | string,
  upColor?: string,
  downColor?: string,
): string | undefined {
  const [flash, setFlash] = useState<string | undefined>(undefined);
  const prevRef = useRef<number | string | undefined>(undefined);
  const timeoutRef = useRef<number | undefined>(undefined);

  useEffect(() => {
    const prev = prevRef.current;
    prevRef.current = value;
    if (prev === undefined || prev === value) return;

    let color: string | undefined = upColor;
    if (typeof value === 'number' && typeof prev === 'number') {
      color = value > prev ? upColor : downColor;
    }
    if (!color) return;

    setFlash(color);
    if (timeoutRef.current) window.clearTimeout(timeoutRef.current);
    timeoutRef.current = window.setTimeout(() => setFlash(undefined), 800);
  }, [value, upColor, downColor]);

  useEffect(() => () => {
    if (timeoutRef.current) window.clearTimeout(timeoutRef.current);
  }, []);

  return flash;
}
