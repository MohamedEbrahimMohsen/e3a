import { useNavigate } from 'react-router-dom';
import { findByName, workspaceRows } from '../../lib/catalog';
import type { WorkspaceRow } from '../../lib/types';

const gridColumns = '2.2fr 0.9fr 1.1fr 0.9fr 0.9fr 1.1fr 1.6fr';

function statusChipStyle(status: WorkspaceRow['status']): React.CSSProperties {
  if (status === 'Published') {
    return { color: 'var(--success)', background: 'rgba(52,211,153,0.1)', border: '1px solid rgba(52,211,153,0.3)' };
  }
  if (status === 'Rejected') {
    return { color: 'var(--danger)', background: 'rgba(248,113,113,0.1)', border: '1px solid rgba(248,113,113,0.3)' };
  }
  return { color: 'var(--text-secondary)', background: 'var(--surface-elevated)', border: '1px solid var(--border)' };
}

function LimitMeter({ label, used, max, critical }: { label: string; used: number; max: number; critical: boolean }) {
  const color = critical ? 'var(--danger)' : 'var(--primary)';
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 7, width: 220 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12 }}>
        <span style={{ color: 'var(--text-secondary)' }}>{label}</span>
        <span className="mono" style={{ color: critical ? 'var(--danger)' : 'var(--text-soft)' }}>{used} / {max}</span>
      </div>
      <div style={{ height: 5, borderRadius: 999, background: 'var(--surface-elevated)', overflow: 'hidden' }}>
        <div style={{ width: `${(used / max) * 100}%`, height: '100%', borderRadius: 999, background: color }} />
      </div>
    </div>
  );
}

export function WorkspacePage() {
  const navigate = useNavigate();

  const editTarget = (row: WorkspaceRow) => (row.type === 'Team' ? '/workspace/new-team' : '/workspace/new-engineer');
  const actionTarget = (row: WorkspaceRow) => (row.action === 'View report' ? '/workspace/publish?mode=rejected' : '/workspace/publish');
  const view = (row: WorkspaceRow) => {
    const item = findByName(row.name);
    navigate(item ? `/${item.team ? 't' : 'e'}/${item.name}` : editTarget(row));
  };

  return (
    <div className="page fade-in" style={{ gap: 28 }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 32 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          <h1 style={{ fontSize: 26, fontWeight: 700 }}>My workspace</h1>
          <div style={{ display: 'flex', gap: 32 }}>
            <LimitMeter label="Engineers" used={12} max={50} critical={false} />
            <LimitMeter label="Teams" used={9} max={10} critical />
          </div>
        </div>
        <div style={{ display: 'flex', gap: 12 }}>
          <button onClick={() => navigate('/workspace/new-team')} className="btn-secondary">+ New Team</button>
          <button onClick={() => navigate('/workspace/new-engineer')} className="btn-primary">+ New Engineer</button>
        </div>
      </div>
      <div className="card" style={{ overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: gridColumns, gap: 16, padding: '12px 24px', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.08em', borderBottom: '1px solid var(--border)' }}>
          <span>Name</span><span>Type</span><span>Status</span><span>Version</span><span>Installs</span><span>Updated</span><span style={{ textAlign: 'right' }}>Actions</span>
        </div>
        {workspaceRows.map(row => (
          <div key={row.name} className="hover-row" style={{ display: 'grid', gridTemplateColumns: gridColumns, gap: 16, padding: '16px 24px', fontSize: 13, alignItems: 'center', borderBottom: '1px solid var(--surface-elevated)' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <span style={{ fontSize: 16 }}>{row.emoji}</span>
              <span className="mono" style={{ fontSize: 13 }}>{row.name}</span>
            </span>
            <span style={{ color: 'var(--text-secondary)' }}>{row.type}</span>
            <span><span style={{ fontSize: 11.5, fontWeight: 600, borderRadius: 999, padding: '3px 11px', ...statusChipStyle(row.status) }}>{row.status}</span></span>
            <span className="mono" style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{row.version}</span>
            <span className="mono" style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{row.installs}</span>
            <span style={{ color: 'var(--text-muted)', fontSize: 12.5 }}>{row.updated}</span>
            <span style={{ display: 'flex', gap: 14, justifyContent: 'flex-end', fontSize: 12.5 }}>
              <span onClick={() => navigate(editTarget(row))} className="link-violet">Edit</span>
              <span onClick={() => navigate(actionTarget(row))} className="link-accent-hover" style={{ color: 'var(--accent)', cursor: 'pointer' }}>{row.action}</span>
              <span onClick={() => view(row)} className="link-quiet" style={{ color: 'var(--text-secondary)', cursor: 'pointer' }}>View</span>
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
