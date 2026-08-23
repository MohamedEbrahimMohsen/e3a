import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { EngineerCard } from '../../components/EngineerCard';
import { allItems, engineers, filterTagNames, teams } from '../../lib/catalog';
import { useToast } from '../../app/ToastContext';

const segments = ['All', 'Engineers', 'Teams'] as const;
type Segment = (typeof segments)[number];
const sortOptions = ['Most installed', 'Newest'] as const;
const PAGE_SIZE = 9;

export function CatalogPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const { showToast } = useToast();
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<(typeof sortOptions)[number]>('Most installed');

  const segment = (searchParams.get('seg') ?? 'All') as Segment;
  const activeTags = searchParams.getAll('tag');

  const setSegment = (next: Segment) => {
    const params = new URLSearchParams(searchParams);
    if (next === 'All') { params.delete('seg'); } else { params.set('seg', next); }
    setSearchParams(params, { replace: true });
  };

  const toggleTag = (tag: string) => {
    const params = new URLSearchParams(searchParams);
    const nextTags = activeTags.includes(tag) ? activeTags.filter(t => t !== tag) : [...activeTags, tag];
    params.delete('tag');
    nextTags.forEach(t => params.append('tag', t));
    setSearchParams(params, { replace: true });
  };

  const items = useMemo(() => {
    let pool = segment === 'Engineers' ? engineers : segment === 'Teams' ? teams : allItems;
    if (activeTags.length > 0) {
      pool = pool.filter(item => item.tags.some(tag => activeTags.includes(tag)));
    }
    const trimmed = query.trim().toLowerCase();
    if (trimmed.length > 0) {
      pool = pool.filter(item => `${item.name} ${item.description} ${item.author}`.toLowerCase().includes(trimmed));
    }
    if (sort === 'Most installed') {
      pool = [...pool].sort((first, second) => second.installs - first.installs);
    }
    return pool.slice(0, PAGE_SIZE);
  }, [segment, activeTags, query, sort]);

  const pageButtonStyle: React.CSSProperties = { width: 32, height: 32, display: 'flex', alignItems: 'center', justifyContent: 'center', borderRadius: 8, border: '1px solid var(--border)', color: 'var(--text-secondary)', cursor: 'pointer' };

  return (
    <div className="page fade-in" style={{ gap: 24 }}>
      <div className="hover-border" style={{ display: 'flex', alignItems: 'center', gap: 14, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12, padding: '4px 18px' }}>
        <span style={{ color: 'var(--text-muted)', fontSize: 15 }}>⌕</span>
        <input value={query} onChange={event => setQuery(event.target.value)} placeholder="Search engineers and teams…" style={{ flex: 1, background: 'transparent', border: 'none', color: 'var(--text)', fontSize: 14.5, padding: '11px 0' }} />
        <span className="mono" style={{ fontSize: 11, color: 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: 6, padding: '2px 7px' }}>/</span>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 999, padding: 3 }}>
          {segments.map(label => (
            <span key={label} onClick={() => setSegment(label)} style={{ fontSize: 13, fontWeight: segment === label ? 600 : 400, padding: '6px 16px', borderRadius: 999, background: segment === label ? 'var(--primary)' : 'transparent', color: segment === label ? '#fff' : 'var(--text-secondary)', cursor: 'pointer', transition: 'all 0.15s ease' }}>{label}</span>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {filterTagNames.map(tag => {
            const active = activeTags.includes(tag);
            return (
              <span key={tag} onClick={() => toggleTag(tag)} className="hover-border-violet" style={{ fontSize: 12.5, color: active ? '#fff' : 'var(--text-secondary)', background: active ? 'var(--primary)' : 'var(--surface)', border: `1px solid ${active ? 'var(--primary)' : 'var(--border)'}`, borderRadius: 999, padding: '5px 14px', cursor: 'pointer' }}># {tag}</span>
            );
          })}
        </div>
        <div onClick={() => setSort(sort === 'Most installed' ? 'Newest' : 'Most installed')} className="hover-border" style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 999, padding: '7px 16px', fontSize: 13, color: 'var(--text-secondary)', cursor: 'pointer' }}>
          Sort: <span style={{ color: 'var(--text)', fontWeight: 500 }}>{sort}</span> <span style={{ color: 'var(--text-muted)' }}>▾</span>
        </div>
      </div>
      {items.length > 0 ? (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 20 }}>
            {items.map(item => <EngineerCard key={item.name} item={item} />)}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, marginTop: 12, fontSize: 13 }}>
            <span style={{ color: 'var(--text-muted)', padding: '7px 12px' }}>← Prev</span>
            <span style={{ ...pageButtonStyle, background: 'var(--primary)', border: 'none', color: '#fff', fontWeight: 600 }}>1</span>
            {['2', '3'].map(page => <span key={page} onClick={() => showToast('Only page 1 has data in this prototype')} className="hover-border-violet" style={pageButtonStyle}>{page}</span>)}
            <span style={{ color: 'var(--text-muted)' }}>…</span>
            <span onClick={() => showToast('Only page 1 has data in this prototype')} className="hover-border-violet" style={pageButtonStyle}>24</span>
            <span onClick={() => showToast('Only page 1 has data in this prototype')} className="link-quiet" style={{ color: 'var(--text-secondary)', padding: '7px 12px', cursor: 'pointer' }}>Next →</span>
          </div>
        </>
      ) : (
        <div className="fade-in" style={{ padding: '72px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 14, textAlign: 'center' }}>
          <span style={{ width: 52, height: 52, borderRadius: 14, background: 'var(--surface)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 22, color: 'var(--text-muted)' }}>⌕</span>
          <span style={{ fontSize: 16, fontWeight: 700 }}>No results for "{query}"</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)', maxWidth: 300, lineHeight: 1.6 }}>Try fewer words, or clear the tag filters — or compose this engineer yourself.</span>
          <div style={{ display: 'flex', gap: 10, marginTop: 6 }}>
            <button onClick={() => { setQuery(''); setSearchParams(new URLSearchParams(), { replace: true }); }} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13 }}>Clear filters</button>
            <button onClick={() => navigate('/workspace/new-engineer')} className="btn-primary" style={{ padding: '8px 18px', fontSize: 13 }}>Compose it</button>
          </div>
        </div>
      )}
    </div>
  );
}
