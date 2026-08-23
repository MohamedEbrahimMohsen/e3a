import { useEffect, useRef, useState } from 'react';
import { copyToClipboard } from '../lib/clipboard';
import { marketplaceAddCommand } from '../lib/config';

const COPIED_FEEDBACK_MS = 1400;

export function InstallBlock({ line2, single = false }: { line2: string; single?: boolean }) {
  const line1 = marketplaceAddCommand();
  const [copied, setCopied] = useState(-1);
  const timerRef = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(timerRef.current), []);

  const copy = (index: number, text: string) => {
    copyToClipboard(text);
    setCopied(index);
    window.clearTimeout(timerRef.current);
    timerRef.current = window.setTimeout(() => setCopied(-1), COPIED_FEEDBACK_MS);
  };

  const row = (index: number, text: string, bordered: boolean) => (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, padding: '12px 16px', borderBottom: bordered ? '1px solid var(--surface-elevated)' : 'none' }}>
      <span style={{ color: 'var(--code-text)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
        <span style={{ color: 'var(--text-muted)' }}>$ </span>{text}
      </span>
      <button onClick={() => copy(index, text)} className="copy-button">{copied === index ? 'copied ✓' : 'copy'}</button>
    </div>
  );

  return (
    <div className="mono" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 12, overflow: 'hidden', fontSize: 12.5 }}>
      {!single && row(0, line1, true)}
      {row(1, line2, false)}
    </div>
  );
}
