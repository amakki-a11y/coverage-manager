import { useEffect } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { THEME } from '../theme';
import guideMd from '../../../docs/USER_GUIDE.md?raw';

/**
 * Full-screen overlay that renders `docs/USER_GUIDE.md` so the dealer can read
 * the app docs without leaving the browser. Opened from the 📖 button in the
 * top bar; closes on Escape or overlay click.
 */
interface Props {
  open: boolean;
  onClose: () => void;
}

export function UserGuideOverlay({ open, onClose }: Props) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      onClick={onClose}
      style={{
        position: 'fixed',
        inset: 0,
        background: THEME.shadowOverlay,
        zIndex: 10_002,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 24,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-label="User guide"
        style={{
          background: THEME.bg2,
          border: `1px solid ${THEME.border}`,
          borderRadius: 8,
          boxShadow: THEME.shadowModal,
          width: 'min(960px, 100%)',
          maxHeight: '90vh',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <div style={{
          display: 'flex',
          alignItems: 'center',
          padding: '14px 20px',
          borderBottom: `1px solid ${THEME.border}`,
        }}>
          <div style={{ color: THEME.t1, fontSize: 15, fontWeight: 700, flex: 1 }}>
            {'\uD83D\uDCD6'} User Guide
          </div>
          <button
            onClick={onClose}
            style={{
              padding: '6px 12px',
              borderRadius: 4,
              border: `1px solid ${THEME.border}`,
              background: 'transparent',
              color: THEME.t2,
              cursor: 'pointer',
              fontSize: 12,
              fontWeight: 600,
            }}
          >
            Close (Esc)
          </button>
        </div>
        <div style={{
          padding: '20px 28px',
          overflow: 'auto',
          color: THEME.t1,
          fontSize: 13,
          lineHeight: 1.6,
        }}>
          <div className="user-guide-md">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>
              {guideMd}
            </ReactMarkdown>
          </div>
        </div>
      </div>
      <style>{`
        .user-guide-md h1 { font-size: 22px; font-weight: 700; margin: 0 0 14px; color: ${THEME.t1}; }
        .user-guide-md h2 { font-size: 17px; font-weight: 700; margin: 24px 0 10px; color: ${THEME.t1}; padding-bottom: 4px; border-bottom: 1px solid ${THEME.border}; }
        .user-guide-md h3 { font-size: 14px; font-weight: 700; margin: 18px 0 8px; color: ${THEME.t1}; }
        .user-guide-md h4 { font-size: 13px; font-weight: 700; margin: 14px 0 6px; color: ${THEME.t2}; }
        .user-guide-md p  { margin: 0 0 10px; color: ${THEME.t2}; }
        .user-guide-md ul, .user-guide-md ol { margin: 0 0 12px; padding-left: 24px; color: ${THEME.t2}; }
        .user-guide-md li { margin-bottom: 4px; }
        .user-guide-md code { background: ${THEME.bg3}; padding: 1px 5px; border-radius: 3px; font-size: 12px; color: ${THEME.blue}; font-family: 'JetBrains Mono', ui-monospace, Menlo, monospace; }
        .user-guide-md pre { background: ${THEME.bg3}; padding: 10px 14px; border-radius: 6px; overflow-x: auto; margin: 0 0 12px; border: 1px solid ${THEME.border}; }
        .user-guide-md pre code { background: transparent; padding: 0; color: ${THEME.t1}; font-size: 11px; line-height: 1.55; }
        .user-guide-md table { border-collapse: collapse; margin: 0 0 14px; font-size: 12px; width: 100%; }
        .user-guide-md th, .user-guide-md td { border: 1px solid ${THEME.border}; padding: 6px 10px; text-align: left; color: ${THEME.t2}; }
        .user-guide-md th { background: ${THEME.bg3}; color: ${THEME.t1}; font-weight: 700; }
        .user-guide-md a { color: ${THEME.blue}; text-decoration: none; }
        .user-guide-md a:hover { text-decoration: underline; }
        .user-guide-md strong { color: ${THEME.t1}; }
        .user-guide-md blockquote { border-left: 3px solid ${THEME.amber}; padding: 6px 14px; margin: 0 0 12px; color: ${THEME.t2}; background: ${THEME.bg3}; border-radius: 4px; }
        .user-guide-md blockquote p:last-child { margin-bottom: 0; }
        .user-guide-md hr { border: 0; border-top: 1px solid ${THEME.border}; margin: 24px 0; }
      `}</style>
    </div>
  );
}
