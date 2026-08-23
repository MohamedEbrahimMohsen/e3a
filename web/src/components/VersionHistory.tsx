import { useEffect, useRef, useState } from 'react';
import type { VersionInfo } from '../lib/types';
import { copyToClipboard } from '../lib/clipboard';
import { pinnedMarketplaceCommand } from '../lib/config';

const COPIED_FEEDBACK_MS = 1400;

export function VersionHistory({ pluginName, versions }: { pluginName: string; versions: VersionInfo[] }) {
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  const [copiedIndex, setCopiedIndex] = useState<number | null>(null);
  const timerRef = useRef<number | undefined>(undefined);

  useEffect(() => () => window.clearTimeout(timerRef.current), []);

  const copyPinCommand = (event: React.MouseEvent, index: number, command: string) => {
    event.stopPropagation();
    copyToClipboard(command);
    setCopiedIndex(index);
    window.clearTimeout(timerRef.current);
    timerRef.current = window.setTimeout(() => setCopiedIndex(null), COPIED_FEEDBACK_MS);
  };

  return (
    <div className="card" style={{ padding: 20 }}>
      <h3 style={{ margin: '0 0 12px', fontSize: 14, fontWeight: 700 }}>Version history</h3>
      {versions.map((versionInfo, index) => {
        const open = openIndex === index;
        const command = pinnedMarketplaceCommand(pluginName, versionInfo.version);
        return (
          <div key={versionInfo.version} style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: '12px 0', borderTop: '1px solid var(--surface-elevated)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <span className="version-badge">{versionInfo.version}</span>
              <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{versionInfo.date}</span>
              <span style={{ marginLeft: 'auto', fontSize: 11.5, color: 'var(--text-muted)' }}>{versionInfo.size}</span>
            </div>
            <div className="mono" style={{ fontSize: 11, color: 'var(--text-muted)' }}>sha256 {versionInfo.sha}</div>
            <span onClick={() => setOpenIndex(open ? null : index)} className="link-accent-hover" style={{ fontSize: 11.5, color: open ? 'var(--accent)' : 'var(--text-muted)' }}>
              {open ? '▾' : '▸'} pin this version
            </span>
            {open && (
              <div className="mono fade-in" style={{ fontSize: 10.5, color: 'var(--code-text)', background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 8, padding: '9px 10px', display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center' }}>
                <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{command}</span>
                <span onClick={event => copyPinCommand(event, index, command)} className="copy-button" style={{ padding: '2px 7px', cursor: 'pointer' }}>{copiedIndex === index ? 'copied ✓' : 'copy'}</span>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
