import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { EngineerCard } from '../../components/EngineerCard';
import { emojiFor, getCatalog, getCatalogTags, type CatalogEngineer, type CatalogSort, type CatalogTag, type PageData } from '../../lib/api';
import type { CatalogItem } from '../../lib/types';

const segments = ['All', 'Engineers', 'Teams'] as const;
type Segment = (typeof segments)[number];
const sortLabels: Record<CatalogSort, string> = { MostInstalled: 'Most installed', Newest: 'Newest' };
const VISIBLE_TAG_FILTERS = 8;

export function toCatalogItem(engineer: CatalogEngineer): CatalogItem {
  return { emoji: emojiFor(engineer.slug), name: engineer.slug, description: engineer.description ?? '', tags: engineer.tags, installs: engineer.installCount };
}

export function CatalogPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<CatalogSort>('MostInstalled');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PageData<CatalogEngineer> | null>(null);
  const [availableTags, setAvailableTags] = useState<CatalogTag[]>([]);
  const [loadFailed, setLoadFailed] = useState(false);

  const segment = (searchParams.get('seg') ?? 'All') as Segment;
  const activeTags = searchParams.getAll('tag');

  useEffect(() => {
    getCatalogTags().then(setAvailableTags).catch(() => setAvailableTags([]));
  }, []);

  useEffect(() => {
    if (segment === 'Teams') {
      return;
    }
    let cancelled = false;
    const timer = window.setTimeout(() => {
      getCatalog({ searchText: query.trim() || undefined, tags: activeTags, sort, pageNumber: page })
        .then(result => { if (!cancelled) { setData(result); setLoadFailed(false); } })
        .catch(() => { if (!cancelled) { setLoadFailed(true); } });
    }, query ? 250 : 0);
    return () => { cancelled = true; window.clearTimeout(timer); };
  }, [segment, query, searchParams, sort, page]);

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
    setPage(1);
  };

  const pageButtonStyle: React.CSSProperties = { width: 32, height: 32, display: 'flex', alignItems: 'center', justifyContent: 'center', borderRadius: 8, border: '1px solid var(--border)', color: 'var(--text-secondary)', cursor: 'pointer' };

  return (
    <div className="page fade-in" style={{ gap: 24 }}>
      <div className="hover-border" style={{ display: 'flex', alignItems: 'center', gap: 14, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12, padding: '4px 18px' }}>
        <span style={{ color: 'var(--text-muted)', fontSize: 15 }}>⌕</span>
        <input value={query} onChange={event => { setQuery(event.target.value); setPage(1); }} placeholder="Search engineers…" style={{ flex: 1, background: 'transparent', border: 'none', color: 'var(--text)', fontSize: 14.5, padding: '11px 0' }} />
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 999, padding: 3 }}>
          {segments.map(label => (
            <span key={label} onClick={() => setSegment(label)} style={{ fontSize: 13, fontWeight: segment === label ? 600 : 400, padding: '6px 16px', borderRadius: 999, background: segment === label ? 'var(--primary)' : 'transparent', color: segment === label ? '#fff' : 'var(--text-secondary)', cursor: 'pointer', transition: 'all 0.15s ease' }}>{label}</span>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {availableTags.slice(0, VISIBLE_TAG_FILTERS).map(tagEntry => {
            const active = activeTags.includes(tagEntry.tag);
            return (
              <span key={tagEntry.tag} onClick={() => toggleTag(tagEntry.tag)} className="hover-border-violet" style={{ fontSize: 12.5, color: active ? '#fff' : 'var(--text-secondary)', background: active ? 'var(--primary)' : 'var(--surface)', border: `1px solid ${active ? 'var(--primary)' : 'var(--border)'}`, borderRadius: 999, padding: '5px 14px', cursor: 'pointer' }}># {tagEntry.tag} <span style={{ opacity: 0.6 }}>{tagEntry.count}</span></span>
            );
          })}
        </div>
        <div onClick={() => { setSort(sort === 'MostInstalled' ? 'Newest' : 'MostInstalled'); setPage(1); }} className="hover-border" style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 999, padding: '7px 16px', fontSize: 13, color: 'var(--text-secondary)', cursor: 'pointer' }}>
          Sort: <span style={{ color: 'var(--text)', fontWeight: 500 }}>{sortLabels[sort]}</span> <span style={{ color: 'var(--text-muted)' }}>▾</span>
        </div>
      </div>
      {segment === 'Teams' ? (
        <div className="fade-in" style={{ padding: '72px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10, textAlign: 'center' }}>
          <span style={{ fontSize: 16, fontWeight: 700 }}>Teams are coming soon</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>Team bundles arrive once publishing lands.</span>
        </div>
      ) : loadFailed ? (
        <div className="fade-in" style={{ padding: '72px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10, textAlign: 'center' }}>
          <span style={{ fontSize: 16, fontWeight: 700 }}>Could not reach the catalog</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>The API is unreachable. Check that it is running, then retry.</span>
          <button onClick={() => { setLoadFailed(false); setPage(1); setQuery(query => query); }} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13, marginTop: 6 }}>Retry</button>
        </div>
      ) : data && data.items.length > 0 ? (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 20 }}>
            {data.items.map(engineer => <EngineerCard key={engineer.id} item={toCatalogItem(engineer)} />)}
          </div>
          {data.totalPages > 1 && (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, marginTop: 12, fontSize: 13 }}>
              <span onClick={() => setPage(Math.max(1, page - 1))} className="link-quiet" style={{ color: page > 1 ? 'var(--text-secondary)' : 'var(--text-muted)', padding: '7px 12px', cursor: 'pointer' }}>← Prev</span>
              {Array.from({ length: data.totalPages }, (_, index) => index + 1).map(pageNumber => (
                <span key={pageNumber} onClick={() => setPage(pageNumber)} className="hover-border-violet" style={pageNumber === page ? { ...pageButtonStyle, background: 'var(--primary)', border: 'none', color: '#fff', fontWeight: 600 } : pageButtonStyle}>{pageNumber}</span>
              ))}
              <span onClick={() => setPage(Math.min(data.totalPages, page + 1))} className="link-quiet" style={{ color: page < data.totalPages ? 'var(--text-secondary)' : 'var(--text-muted)', padding: '7px 12px', cursor: 'pointer' }}>Next →</span>
            </div>
          )}
        </>
      ) : data ? (
        <div className="fade-in" style={{ padding: '72px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 14, textAlign: 'center' }}>
          <span style={{ width: 52, height: 52, borderRadius: 14, background: 'var(--surface)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 22, color: 'var(--text-muted)' }}>⌕</span>
          <span style={{ fontSize: 16, fontWeight: 700 }}>No results{query ? ` for "${query}"` : ''}</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)', maxWidth: 300, lineHeight: 1.6 }}>Try fewer words, or clear the tag filters.</span>
          <button onClick={() => { setQuery(''); setPage(1); setSearchParams(new URLSearchParams(), { replace: true }); }} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13, marginTop: 6 }}>Clear filters</button>
        </div>
      ) : (
        <div style={{ padding: '72px 40px', textAlign: 'center', color: 'var(--text-muted)', fontSize: 13.5 }}>Loading…</div>
      )}
    </div>
  );
}
