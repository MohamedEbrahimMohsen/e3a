import type { ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';
import { useToast } from '../../app/ToastContext';

export function ComposerShell({ title, lastSaved, onSaveDraft, children }: { title: string; lastSaved: string; onSaveDraft: () => void; children: ReactNode }) {
  const navigate = useNavigate();
  const { login } = useAuth();
  const { showToast } = useToast();

  return (
    <div className="fade-in" style={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 48px', height: 64, borderBottom: '1px solid var(--border)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
          <Link to="/" className="gradient-text" style={{ fontWeight: 800, fontSize: 19 }}>e3a</Link>
          <Link to="/workspace" className="link-quiet" style={{ color: 'var(--text-secondary)', fontSize: 13 }}>← Workspace</Link>
          <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>/</span>
          <span style={{ fontSize: 14, fontWeight: 600 }}>{title}</span>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', background: 'var(--surface-elevated)', border: '1px solid var(--border)', borderRadius: 999, padding: '3px 11px' }}>Draft</span>
        </div>
        <div onClick={() => navigate(`/u/${login}`)} className="hover-border-violet" style={{ width: 32, height: 32, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#1d1d23)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: 'var(--text-secondary)', cursor: 'pointer' }}>MD</div>
      </div>
      {children}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '16px 40px', borderTop: '1px solid var(--border)', background: 'var(--bg-deep)', position: 'sticky', bottom: 0 }}>
        <span style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>Last saved {lastSaved}</span>
        <div style={{ display: 'flex', gap: 12 }}>
          <button onClick={() => { onSaveDraft(); showToast('Draft saved'); }} className="btn-secondary" style={{ padding: '9px 22px' }}>Save draft</button>
          <button onClick={() => navigate('/workspace/publish')} className="btn-primary" style={{ padding: '9px 22px' }}>Publish</button>
        </div>
      </div>
    </div>
  );
}
