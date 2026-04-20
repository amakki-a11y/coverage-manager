/* global React */
// Tiny icon set (Lucide-style strokes, 16px). All inherit currentColor.

const Icon = ({ d, size = 16, fill = 'none', strokeWidth = 1.6, style }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill={fill}
    stroke="currentColor" strokeWidth={strokeWidth}
    strokeLinecap="round" strokeLinejoin="round"
    style={style} className="icon">
    {d}
  </svg>
);

const I = {
  grid: () => <Icon d={<><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></>} />,
  list: () => <Icon d={<><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><circle cx="4" cy="6" r="1"/><circle cx="4" cy="12" r="1"/><circle cx="4" cy="18" r="1"/></>} />,
  pulse: () => <Icon d={<polyline points="3 12 7 12 10 6 14 18 17 12 21 12"/>} />,
  wallet: () => <Icon d={<><path d="M3 7a2 2 0 012-2h14v12a2 2 0 01-2 2H5a2 2 0 01-2-2V7z"/><path d="M16 12h3"/></>} />,
  layers: () => <Icon d={<><path d="M12 3L3 8l9 5 9-5-9-5z"/><path d="M3 14l9 5 9-5"/></>} />,
  split: () => <Icon d={<><path d="M4 4h6v16H4z"/><path d="M14 4h6v16h-6z"/></>} />,
  arrows: () => <Icon d={<><path d="M7 7h10M7 7l3-3M7 7l3 3"/><path d="M17 17H7M17 17l-3-3M17 17l-3 3"/></>} />,
  bridge: () => <Icon d={<><path d="M3 12h18"/><path d="M7 12V8a2 2 0 012-2h6a2 2 0 012 2v4"/><path d="M5 16v-4M19 16v-4"/></>} />,
  map: () => <Icon d={<><polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21 3 6"/><line x1="9" y1="3" x2="9" y2="18"/><line x1="15" y1="6" x2="15" y2="21"/></>} />,
  gear: () => <Icon d={<><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 00.3 1.8l.1.1a2 2 0 11-2.8 2.8l-.1-.1a1.7 1.7 0 00-1.8-.3 1.7 1.7 0 00-1 1.5v.2a2 2 0 11-4 0v-.1a1.7 1.7 0 00-1.1-1.6 1.7 1.7 0 00-1.8.3l-.1.1a2 2 0 11-2.8-2.8l.1-.1a1.7 1.7 0 00.3-1.8 1.7 1.7 0 00-1.5-1H3a2 2 0 110-4h.1a1.7 1.7 0 001.6-1.1 1.7 1.7 0 00-.3-1.8l-.1-.1a2 2 0 112.8-2.8l.1.1a1.7 1.7 0 001.8.3H9a1.7 1.7 0 001-1.5V3a2 2 0 114 0v.1a1.7 1.7 0 001 1.5 1.7 1.7 0 001.8-.3l.1-.1a2 2 0 112.8 2.8l-.1.1a1.7 1.7 0 00-.3 1.8v.1a1.7 1.7 0 001.5 1H21a2 2 0 110 4h-.1a1.7 1.7 0 00-1.5 1z"/></>} />,
  bell: () => <Icon d={<><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 01-3.4 0"/></>} />,
  sun: () => <Icon d={<><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></>} />,
  moon: () => <Icon d={<path d="M21 12.8A9 9 0 1111.2 3a7 7 0 009.8 9.8z"/>} />,
  search: () => <Icon d={<><circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></>} />,
  sliders: () => <Icon d={<><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><circle cx="4" cy="12" r="2"/><circle cx="12" cy="10" r="2"/><circle cx="20" cy="14" r="2"/></>} />,
  close: () => <Icon d={<><line x1="6" y1="6" x2="18" y2="18"/><line x1="6" y1="18" x2="18" y2="6"/></>} />,
  check: () => <Icon d={<polyline points="4 12 10 18 20 6"/>} />,
  warn: () => <Icon d={<><path d="M12 2L2 21h20L12 2z"/><line x1="12" y1="9" x2="12" y2="14"/><circle cx="12" cy="17.5" r="0.8" fill="currentColor"/></>} />,
  info: () => <Icon d={<><circle cx="12" cy="12" r="9"/><line x1="12" y1="11" x2="12" y2="16"/><circle cx="12" cy="8" r="0.8" fill="currentColor"/></>} />,
  bolt: () => <Icon d={<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" fill="currentColor" stroke="none"/>} />,
  plus: () => <Icon d={<><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></>} />,
  drag: () => <Icon size={12} d={<><circle cx="9" cy="6" r="1" fill="currentColor"/><circle cx="15" cy="6" r="1" fill="currentColor"/><circle cx="9" cy="12" r="1" fill="currentColor"/><circle cx="15" cy="12" r="1" fill="currentColor"/><circle cx="9" cy="18" r="1" fill="currentColor"/><circle cx="15" cy="18" r="1" fill="currentColor"/></>} />,
  chevLeft: () => <Icon d={<polyline points="15 6 9 12 15 18"/>} />,
  chevRight: () => <Icon d={<polyline points="9 6 15 12 9 18"/>} />,
  chevDown: () => <Icon d={<polyline points="6 9 12 15 18 9"/>} />,
  download: () => <Icon d={<><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></>} />,
  refresh: () => <Icon d={<><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.5 9a9 9 0 0114.9-3.4L23 10M1 14l4.6 4.4A9 9 0 0020.5 15"/></>} />,
  kbd: () => <Icon d={<><rect x="2" y="4" width="20" height="16" rx="2"/><path d="M6 8h2M10 8h2M14 8h2M18 8h2M6 12h2M10 12h8M6 16h12"/></>} />,
  camera: () => <Icon d={<><path d="M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z"/><circle cx="12" cy="13" r="4"/></>} />,
  book: () => <Icon d={<><path d="M2 3h6a4 4 0 014 4v14a3 3 0 00-3-3H2z"/><path d="M22 3h-6a4 4 0 00-4 4v14a3 3 0 013-3h7z"/></>} />,
};

window.I = I;
