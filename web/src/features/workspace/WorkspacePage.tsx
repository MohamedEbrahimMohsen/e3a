import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { emojiFor } from '../../lib/api';
import { listMyEngineers, type Engineer } from '../../lib/workspaceApi';

const gridColumns = '2.4fr 0.9fr 1.1fr 0.9fr 1.1fr 1.6fr';

function statusChipStyle(status: string): React.CSSProperties {
  if (status === 'Published') {
    return { color: 'var(--success)', background: 'rgba(52,211,153,0.1)', border: '1px solid rgba(52,211,153,0.3)' };
  }
  if (status === 'Unlisted') {
    return { color: 'var(--warning)', background: 'rgba(251,191,36,0.1)', border: '1px solid rgba(251,191,36,0.3)' };
  }
  return { color: 'var(--text-secondary)', background: 'var(--surface-elevated)', border: '1px solid var(--border)' };
}

function formatUpdated(updatedAt: string): string {
  return new Date(updatedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

export function WorkspacePage() {
  const navigate = useNavigate();
  const [engineers, setEngineers] = useState<Engineer[]>([]);
  const [status, setStatus] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listMyEngineers()
      .then(result => { if (!cancelled) { setEngineers(result); setStatus('ready'); } })
      .catch(() => { if (!cancelled) { setStatus('failed'); } });
    return () => { cancelled = true; };
  }, [reloadToken]);

  const newEngineerButton = (className: string) => (
    <button onClick={() => navigate('/workspace/new-engineer')} className={className}>+ New Engineer</button>
  );

  return (
    <div className="page fade-in" style={{ gap: 28 }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 32 }}>
        <h1 style={{ fontSize: 26, fontWeight: 700 }}>My workspace</h1>
        {newEngineerButton('btn-primary')}
      </div>
      {status === 'loading' && <div style={{ padding: '72px 40px', textAlign: 'center', color: 'var(--text-muted)', fontSize: 13.5 }}>Loading…</div>}
      {status === 'failed' && (
        <div className="fade-in" style={{ padding: '72px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10, textAlign: 'center' }}>
          <span style={{ fontSize: 16, fontWeight: 700 }}>Could not load your workspace</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>The API is unreachable. Check that it is running, then retry.</span>
          <button onClick={() => { setStatus('loading'); setReloadToken(token => token + 1); }} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13, marginTop: 6 }}>Retry</button>
        </div>
      )}
      {status === 'ready' && engineers.length === 0 && (
        <div className="fade-in" style={{ padding: '72px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, textAlign: 'center' }}>
          <span style={{ fontSize: 16, fontWeight: 700 }}>Nothing here yet</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>Compose your first engineer — upload your .claude folder and publish it.</span>
          {newEngineerButton('btn-primary')}
        </div>
      )}
      {status === 'ready' && engineers.length > 0 && (
        <div className="card" style={{ overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: gridColumns, gap: 16, padding: '12px 24px', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em', borderBottom: '1px solid var(--border)' }}>
            <span>Name</span><span>Type</span><span>Status</span><span>Installs</span><span>Updated</span><span style={{ textAlign: 'right' }}>Actions</span>
          </div>
          {engineers.map(engineer => (
            <div key={engineer.id} className="hover-row" style={{ display: 'grid', gridTemplateColumns: gridColumns, gap: 16, padding: '16px 24px', fontSize: 13, alignItems: 'center', borderBottom: '1px solid var(--surface-elevated)' }}>
              <span style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <span style={{ fontSize: 16 }}>{emojiFor(engineer.slug)}</span>
                <span className="mono" style={{ fontSize: 13 }}>{engineer.slug}</span>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{engineer.displayName}</span>
              </span>
              <span style={{ color: 'var(--text-secondary)' }}>Engineer</span>
              <span><span style={{ fontSize: 11.5, fontWeight: 600, borderRadius: 999, padding: '3px 11px', ...statusChipStyle(engineer.status) }}>{engineer.status}</span></span>
              <span className="mono" style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{engineer.installCount.toLocaleString('en-US')}</span>
              <span style={{ color: 'var(--text-muted)', fontSize: 12.5 }}>{formatUpdated(engineer.updatedAt)}</span>
              <span style={{ display: 'flex', gap: 14, justifyContent: 'flex-end', fontSize: 12.5 }}>
                <span onClick={() => navigate(`/workspace/engineers/${engineer.id}`)} className="link-violet">Edit</span>
                <span onClick={() => navigate(engineer.latestVersionId ? `/workspace/publish?versionId=${engineer.latestVersionId}` : `/workspace/engineers/${engineer.id}`)} className="link-accent-hover" style={{ color: 'var(--accent)', cursor: 'pointer' }}>{engineer.latestVersionId ? 'View status' : 'Publish'}</span>
                <span onClick={() => navigate(engineer.status === 'Published' ? `/e/${engineer.slug}` : `/workspace/engineers/${engineer.id}`)} className="link-quiet" style={{ color: 'var(--text-secondary)', cursor: 'pointer' }}>View</span>
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
