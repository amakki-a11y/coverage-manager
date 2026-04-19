import React from 'react';
import { THEME } from '../theme';
import { useFlashOnChange } from '../hooks/useFlashOnChange';

/**
 * Wraps a numeric table cell and briefly tints its background when `value`
 * changes — green when the number ticks up, red when it ticks down. Uses the
 * `useFlashOnChange` hook internally so it can be dropped into any map/render
 * loop without violating the rules of hooks (which forbids hook calls in the
 * parent's .map callback).
 *
 * `cellStyle` is applied as the base; the flash tint is layered on top for
 * ~800ms and then fades to transparent.
 */
interface Props {
  value: number;
  children?: React.ReactNode;
  cellStyle?: React.CSSProperties;
  as?: 'td' | 'span' | 'div';
  upColor?: string;
  downColor?: string;
  // Passthrough attrs
  colSpan?: number;
  rowSpan?: number;
  title?: string;
}

export function FlashingCell({
  value,
  children,
  cellStyle,
  as = 'td',
  upColor = THEME.badgeGreen,
  downColor = THEME.badgeRed,
  colSpan,
  rowSpan,
  title,
}: Props) {
  const flash = useFlashOnChange(value, upColor, downColor);
  const merged: React.CSSProperties = {
    ...cellStyle,
    transition: 'background-color 0.3s ease',
    background: flash ?? cellStyle?.background,
  };
  const Tag = as as 'td';
  return (
    <Tag style={merged} colSpan={colSpan} rowSpan={rowSpan} title={title}>
      {children ?? (Number.isFinite(value) ? value : '')}
    </Tag>
  );
}
