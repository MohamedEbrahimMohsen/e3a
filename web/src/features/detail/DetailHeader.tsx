import { Link, useNavigate } from 'react-router-dom';
import type { CatalogItem } from '../../lib/types';
import { formatInstalls } from '../../lib/catalog';

export function DetailHeader({ item }: { item: CatalogItem }) {
  const navigate = useNavigate();

  return (
    <>
      <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>
        <Link to="/catalog" className="link-quiet" style={{ color: 'var(--text-secondary)' }}>← Catalog</Link>
        {' / '}
        <span className="mono">{item.name}</span>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
        <div style={{ width: 64, height: 64, borderRadius: 14, background: 'var(--surface-elevated)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 32 }}>{item.emoji}</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <h1 className="mono" style={{ fontSize: 28, fontWeight: 700, letterSpacing: '-0.01em' }}>{item.name}</h1>
            <span className="version-badge" style={{ fontSize: 12, padding: '3px 9px' }}>{item.version}</span>
            {item.team && <span style={{ fontSize: 11, color: 'var(--accent)', background: 'rgba(34,211,238,0.08)', border: '1px solid rgba(34,211,238,0.25)', borderRadius: 999, padding: '3px 10px', textTransform: 'uppercase', letterSpacing: '0.08em' }}>Team</span>}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, fontSize: 13.5, color: 'var(--text-secondary)' }}>
            <span onClick={() => navigate(`/u/${item.author}`)} className="link-author" style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
              <span style={{ width: 16, height: 16, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#26262c)', display: 'inline-block' }} />
              @{item.author}
            </span>
            <span style={{ color: 'var(--text-muted)' }}>·</span>
            <span>{formatInstalls(item.installs)}</span>
          </div>
        </div>
      </div>
    </>
  );
}
