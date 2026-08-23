import { useNavigate } from 'react-router-dom';

export function MetaPanel({ meta, tags }: { meta: { key: string; value: string }[]; tags: string[] }) {
  const navigate = useNavigate();

  return (
    <div className="card" style={{ padding: 20, display: 'flex', flexDirection: 'column', gap: 14, fontSize: 13 }}>
      {meta.map(entry => (
        <div key={entry.key} style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
          <span style={{ color: 'var(--text-muted)' }}>{entry.key}</span>
          <span className="mono" style={{ color: 'var(--text-soft)', fontSize: 12, textAlign: 'right' }}>{entry.value}</span>
        </div>
      ))}
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', paddingTop: 4, borderTop: '1px solid var(--surface-elevated)' }}>
        {tags.map(tag => (
          <span key={tag} onClick={() => navigate(`/catalog?tag=${tag}`)} className="tag-chip" style={{ cursor: 'pointer' }}>{tag}</span>
        ))}
      </div>
    </div>
  );
}
