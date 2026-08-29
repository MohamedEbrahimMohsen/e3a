import { useState } from 'react';
import type { ImportManifest } from '../../lib/workspaceApi';

const sectionHeaderStyle: React.CSSProperties = { display: 'flex', alignItems: 'center', gap: 10, padding: '11px 14px', cursor: 'pointer', fontSize: 13, fontWeight: 600 };
const rowStyle: React.CSSProperties = { display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px', fontSize: 11.5, borderTop: '1px solid var(--surface-elevated)', flexWrap: 'wrap' };
const pathStyle: React.CSSProperties = { color: 'var(--text-soft)' };
const reasonStyle: React.CSSProperties = { color: 'var(--text-muted)', fontSize: 11 };

export function ImportManifestPanel({ manifest, onReplace }: { manifest: ImportManifest; onReplace: () => void }) {
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

  const toggle = (key: string) => setExpanded({ ...expanded, [key]: !expanded[key] });
  const chevron = (key: string) => <span style={{ marginLeft: 'auto', color: 'var(--text-muted)', fontSize: 11 }}>{expanded[key] ? '▾' : '▸'}</span>;

  const section = (key: string, label: string, color: string, marker: string, count: number, rows: React.ReactNode) => (
    <div className="card" style={{ overflow: 'hidden' }}>
      <div onClick={() => toggle(key)} className="hover-row" style={sectionHeaderStyle}>
        <span style={{ color }}>{marker}</span>
        <span>{label}</span>
        <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>· {count}</span>
        {chevron(key)}
      </div>
      {expanded[key] && count > 0 && <div className="mono fade-in">{rows}</div>}
    </div>
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {section('imported', 'Imported', 'var(--success)', '✓', manifest.imported.length, manifest.imported.map(item => (
        <div key={item.targetPath} style={rowStyle}>
          <span style={pathStyle}>{item.sourcePath} → {item.targetPath}</span>
          <span style={{ marginLeft: 'auto', fontSize: 10.5, color: 'var(--text-muted)', background: 'var(--surface-elevated)', borderRadius: 6, padding: '2px 8px' }}>{item.category}</span>
        </div>
      )))}
      {section('converted', 'Converted', 'var(--accent)', 'ⓘ', manifest.converted.length, manifest.converted.map(item => (
        <div key={item.targetPath} style={rowStyle}>
          <span style={pathStyle}>{item.sourcePath} → {item.targetPath}</span>
          <span style={reasonStyle}>{item.reason}</span>
        </div>
      )))}
      {section('skipped', 'Skipped', 'var(--text-muted)', '—', manifest.skipped.length, manifest.skipped.map(item => (
        <div key={item.sourcePath} style={rowStyle}>
          <span style={pathStyle}>{item.sourcePath}</span>
          <span style={reasonStyle}>{item.reason}</span>
        </div>
      )))}
      {manifest.hookWarnings.length > 0 && (
        <div style={{ background: 'rgba(251,191,36,0.06)', border: '1px solid rgba(251,191,36,0.3)', borderRadius: 12, padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 8 }}>
          <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--warning)' }}>⚠ Includes {manifest.hookWarnings.length} hooks that run automatically</span>
          {manifest.hookWarnings.map(warning => (
            <span key={`${warning.event}-${warning.command}`} className="mono" style={{ fontSize: 11, color: 'var(--text-secondary)' }}>{warning.event} · {warning.matcher ?? '—'} · {warning.command ?? '—'}</span>
          ))}
        </div>
      )}
      {manifest.claudeMdSnippet !== null && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <span onClick={() => toggle('snippet')} className="link-quiet" style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>view snippet {expanded.snippet ? '▾' : '▸'}</span>
          {expanded.snippet && <div className="code-block fade-in" style={{ fontSize: 11.5, whiteSpace: 'pre-wrap' }}>{manifest.claudeMdSnippet}</div>}
        </div>
      )}
      {manifest.strippedPaths.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <span onClick={() => toggle('stripped')} className="link-quiet" style={{ fontSize: 12, color: 'var(--text-muted)' }}>{manifest.strippedPaths.length} local files stripped {expanded.stripped ? '▾' : '▸'}</span>
          {expanded.stripped && manifest.strippedPaths.map(path => <span key={path} className="mono fade-in" style={{ fontSize: 11, color: 'var(--text-muted)' }}>{path}</span>)}
        </div>
      )}
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <button onClick={onReplace} className="btn-secondary" style={{ padding: '7px 16px', fontSize: 12.5 }}>Replace upload</button>
      </div>
    </div>
  );
}
