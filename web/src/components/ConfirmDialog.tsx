import { useEffect } from 'react';
import { THEME } from '../theme';

/**
 * Modal confirm prompt used before destructive actions (delete mapping,
 * delete account, delete schedule). Blocks the underlying UI and focuses
 * the user on whether they really meant to proceed.
 */
interface Props {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  open, title, message,
  confirmLabel = 'Confirm', cancelLabel = 'Cancel',
  danger = true,
  onConfirm, onCancel,
}: Props) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancel();
      if (e.key === 'Enter') onConfirm();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onConfirm, onCancel]);

  if (!open) return null;

  return (
    <div
      onClick={onCancel}
      style={{
        position: 'fixed',
        inset: 0,
        background: THEME.shadowOverlay,
        zIndex: 1000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        style={{
          background: THEME.bg2,
          border: `1px solid ${THEME.border}`,
          borderRadius: 8,
          padding: 20,
          minWidth: 360,
          maxWidth: 480,
          boxShadow: THEME.shadowModal,
        }}
      >
        <div style={{
          color: THEME.t1,
          fontSize: 15,
          fontWeight: 700,
          marginBottom: 8,
        }}>
          {title}
        </div>
        <div style={{
          color: THEME.t2,
          fontSize: 13,
          lineHeight: 1.5,
          marginBottom: 20,
          whiteSpace: 'pre-wrap',
        }}>
          {message}
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button
            onClick={onCancel}
            style={{
              padding: '6px 14px',
              borderRadius: 4,
              border: `1px solid ${THEME.border}`,
              background: 'transparent',
              color: THEME.t2,
              cursor: 'pointer',
              fontSize: 12,
              fontWeight: 600,
            }}
          >
            {cancelLabel}
          </button>
          <button
            onClick={onConfirm}
            autoFocus
            style={{
              padding: '6px 14px',
              borderRadius: 4,
              border: 'none',
              background: danger ? THEME.red : THEME.blue,
              color: '#fff',
              cursor: 'pointer',
              fontSize: 12,
              fontWeight: 700,
            }}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
