import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { InstallBlock } from '../../components/InstallBlock';
import { ApiError, emojiFor, getCatalogEngineer, type CatalogEngineerDetail } from '../../lib/api';
import { installCommand } from '../../lib/config';
import { formatInstalls } from '../../lib/catalog';
import { useReport } from '../../app/ReportContext';
import { NotFoundPage } from '../notfound/NotFoundPage';

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export function EngineerDetailPage() {
  const { name = '' } = useParams();
  const { openReport } = useReport();
  const [engineer, setEngineer] = useState<CatalogEngineerDetail | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'notfound' | 'error'>('loading');
  const [hooksOpen, setHooksOpen] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setStatus('loading');
    getCatalogEngineer(name)
      .then(result => { if (!cancelled) { setEngineer(result); setStatus('ready'); } })
      .catch(error => { if (!cancelled) { setStatus(error instanceof ApiError && error.status === 404 ? 'notfound' : 'error'); } });
    return () => { cancelled = true; };
  }, [name]);

  if (status === 'notfound') {
    return <NotFoundPage />;
  }
  if (status === 'loading') {
    return <div className="page" style={{ padding: '72px 48px', textAlign: 'center', color: 'var(--text-muted)', fontSize: 13.5 }}>Loading…</div>;
  }
  if (status === 'error' || !engineer) {
    return (
      <div className="page fade-in" style={{ padding: '72px 48px', alignItems: 'center', gap: 10, textAlign: 'center' }}>
        <span style={{ fontSize: 16, fontWeight: 700 }}>Could not load this engineer</span>
        <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>The API is unreachable. Check that it is running.</span>
      </div>
    );
  }

  return (
    <div className="page fade-in" style={{ gap: 28 }}>
      <div style={{ fontSize: 13, color: 'var(--text-muted)' }}>
        <Link to="/catalog" className="link-quiet" style={{ color: 'var(--text-secondary)' }}>← Catalog</Link>
        {' / '}
        <span className="mono">{engineer.slug}</span>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
        <div style={{ width: 64, height: 64, borderRadius: 14, background: 'var(--surface-elevated)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 32 }}>{emojiFor(engineer.slug)}</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <h1 className="mono" style={{ fontSize: 28, fontWeight: 700, letterSpacing: '-0.01em' }}>{engineer.slug}</h1>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, fontSize: 13.5, color: 'var(--text-secondary)' }}>
            <span>{engineer.displayName}</span>
            <span style={{ color: 'var(--text-muted)' }}>·</span>
            <span>{formatInstalls(engineer.installCount)}</span>
          </div>
        </div>
        <button type="button" onClick={() => openReport({ itemType: 'Engineer', itemId: engineer.id, label: engineer.slug })} className="link-danger-hover" style={{ marginLeft: 'auto', background: 'transparent', border: 'none', padding: 0, fontSize: 13, color: 'var(--text-muted)' }}>Report</button>
      </div>
      <InstallBlock line2={installCommand(engineer.slug)} />
      {engineer.hookWarnings.length > 0 && (
        <div style={{ background: 'rgba(251,191,36,0.06)', border: '1px solid rgba(251,191,36,0.3)', borderRadius: 12, padding: '14px 18px' }}>
          <button type="button" onClick={() => setHooksOpen(!hooksOpen)} aria-expanded={hooksOpen} style={{ width: '100%', display: 'flex', alignItems: 'center', gap: 10, background: 'transparent', border: 'none', padding: 0, textAlign: 'left', fontSize: 13.5, color: 'var(--warning)', cursor: 'pointer' }}>
            <span>⚠</span>
            <span style={{ fontWeight: 600 }}>Includes {engineer.hookWarnings.length} {engineer.hookWarnings.length > 1 ? 'hooks that run' : 'hook that runs'} automatically</span>
            <span style={{ marginLeft: 'auto', color: 'var(--text-muted)', fontSize: 12 }}>{hooksOpen ? '▴ hide' : '▾ inspect'}</span>
          </button>
          {hooksOpen && (
            <div className="fade-in" style={{ marginTop: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
              {engineer.hookWarnings.map((hook, index) => (
                <div key={index} className="mono" style={{ fontSize: 12, color: 'var(--text-soft)', background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 8, padding: '8px 12px' }}>
                  <span style={{ color: 'var(--warning)' }}>{hook.event}</span>
                  {hook.matcher && <span style={{ color: 'var(--text-muted)' }}> · matcher: {hook.matcher}</span>}
                  {hook.command && <span style={{ color: 'var(--text-muted)' }}> · {hook.command}</span>}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 340px', gap: 32, alignItems: 'start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
          <div style={{ fontSize: 14.5, lineHeight: 1.7, color: 'var(--text-soft)', display: 'flex', flexDirection: 'column', gap: 14 }}>
            <p>{engineer.description ?? 'No description provided.'}</p>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h3 style={{ fontSize: 16, fontWeight: 700 }}>Versions</h3>
            <div style={{ fontSize: 13.5, color: 'var(--text-secondary)', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 12, padding: '16px 20px', lineHeight: 1.6 }}>
              No published plugin versions yet — installable versions arrive with the publishing pipeline.
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <div className="card" style={{ padding: 20, display: 'flex', flexDirection: 'column', gap: 14, fontSize: 13 }}>
            {[{ key: 'Published', value: formatDate(engineer.createdAt) }, { key: 'Last updated', value: formatDate(engineer.updatedAt) }, { key: 'Installs', value: engineer.installCount.toLocaleString('en-US') }].map(entry => (
              <div key={entry.key} style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
                <span style={{ color: 'var(--text-muted)' }}>{entry.key}</span>
                <span className="mono" style={{ color: 'var(--text-soft)', fontSize: 12, textAlign: 'right' }}>{entry.value}</span>
              </div>
            ))}
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', paddingTop: 4, borderTop: '1px solid var(--surface-elevated)' }}>
              {engineer.tags.map(tag => (
                <Link key={tag} to={`/catalog?tag=${tag}`} className="tag-chip" style={{ cursor: 'pointer' }}>{tag}</Link>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
