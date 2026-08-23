import { useNavigate } from 'react-router-dom';
import type { CatalogItem } from '../lib/types';
import { formatInstalls } from '../lib/catalog';

const SPARKLINE_THRESHOLD = 50;

function Sparkline({ name }: { name: string }) {
  const seed = name.length * 2.7;
  const points: number[] = [];
  let value = 10;
  for (let index = 0; index < 12; index++) {
    value = Math.max(2, value + Math.sin(index * 1.9 + seed) * 3 + 0.6);
    points.push(value);
  }
  const max = Math.max(...points);
  const path = points.map((point, index) => `${(index * (64 / 11)).toFixed(1)},${(19 - (point / max) * 16).toFixed(1)}`).join(' ');
  return (
    <svg width={64} height={20} viewBox="0 0 64 20" style={{ display: 'block', flexShrink: 0 }}>
      <polyline points={path} fill="none" stroke="var(--accent)" strokeWidth={1.5} strokeLinejoin="round" strokeLinecap="round" />
    </svg>
  );
}

export function EngineerCard({ item }: { item: CatalogItem }) {
  const navigate = useNavigate();
  const open = () => navigate(`/${item.team ? 't' : 'e'}/${item.name}`);
  const openAuthor = (event: React.MouseEvent) => { event.stopPropagation(); navigate(`/u/${item.author}`); };
  const openTag = (event: React.MouseEvent, tag: string) => { event.stopPropagation(); navigate(`/catalog?tag=${tag}`); };

  return (
    <div onClick={open} className="card card-clickable" style={{ padding: 18, display: 'flex', flexDirection: 'column', gap: 12, height: '100%' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
        <div style={{ width: 44, height: 44, borderRadius: 10, background: 'var(--surface-elevated)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 22, flexShrink: 0 }}>{item.emoji}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div className="mono" style={{ fontWeight: 600, fontSize: 15, color: 'var(--text)' }}>{item.name}</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 3 }}>
            <span style={{ width: 14, height: 14, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#26262c)', display: 'inline-block', flexShrink: 0 }} />
            <span onClick={openAuthor} className="link-author" style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>@{item.author}</span>
          </div>
        </div>
        <span className="version-badge">{item.version}</span>
      </div>
      <div style={{ fontSize: 13, lineHeight: 1.55, color: 'var(--text-secondary)' }}>{item.description}</div>
      {item.team && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{ display: 'flex', paddingLeft: 6 }}>
            {(item.members ?? []).map((memberEmoji, index) => (
              <span key={index} style={{ width: 26, height: 26, borderRadius: 8, background: 'var(--surface-elevated)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13, marginLeft: -6 }}>{memberEmoji}</span>
            ))}
          </div>
          <span style={{ fontSize: 10.5, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em' }}>Team · {(item.members ?? []).length} engineers</span>
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 'auto', gap: 12 }}>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {item.tags.map(tag => (
            <span key={tag} onClick={event => openTag(event, tag)} className="tag-chip">{tag}</span>
          ))}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
          {item.installs >= SPARKLINE_THRESHOLD && <Sparkline name={item.name} />}
          <span style={{ fontSize: 12, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{formatInstalls(item.installs)}</span>
        </div>
      </div>
    </div>
  );
}
