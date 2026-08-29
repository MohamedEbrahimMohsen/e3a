import type { ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';
import { initialsFor } from '../../lib/initials';

const AVATAR_SIZE = 32;

interface ComposerShellProps {
  title: string;
  lastSaved: string;
  onSaveDraft: () => void;
  children: ReactNode;
  onPublish?: () => void;
  publishDisabled?: boolean;
  publishLabel?: string;
  statusLabel?: string;
}

export function ComposerShell({ title, lastSaved, onSaveDraft, children, onPublish, publishDisabled = false, publishLabel = 'Publish', statusLabel = 'Draft' }: ComposerShellProps) {
  const navigate = useNavigate();
  const { login, user } = useAuth();

  const publish = onPublish ?? (() => navigate('/workspace/publish'));
  const openProfile = () => navigate(`/u/${login}`);
  const avatarStyle: React.CSSProperties = { width: AVATAR_SIZE, height: AVATAR_SIZE, borderRadius: '50%', border: '1px solid var(--border)', cursor: 'pointer' };

  return (
    <div className="fade-in" style={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 48px', height: 64, borderBottom: '1px solid var(--border)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
          <Link to="/" className="gradient-text" style={{ fontWeight: 800, fontSize: 19 }}>e3a</Link>
          <Link to="/workspace" className="link-quiet" style={{ color: 'var(--text-secondary)', fontSize: 13 }}>← Workspace</Link>
          <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>/</span>
          <span style={{ fontSize: 14, fontWeight: 600 }}>{title}</span>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', background: 'var(--surface-elevated)', border: '1px solid var(--border)', borderRadius: 999, padding: '3px 11px' }}>{statusLabel}</span>
        </div>
        {user?.avatarUrl
          ? <img onClick={openProfile} src={user.avatarUrl} alt="" width={AVATAR_SIZE} height={AVATAR_SIZE} className="hover-border-violet" style={avatarStyle} />
          : <div onClick={openProfile} className="hover-border-violet" style={{ ...avatarStyle, background: 'linear-gradient(135deg,#3f3f46,#1d1d23)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: 'var(--text-secondary)' }}>{initialsFor(user?.displayName ?? login)}</div>}
      </div>
      {children}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '16px 40px', borderTop: '1px solid var(--border)', background: 'var(--bg-deep)', position: 'sticky', bottom: 0 }}>
        <span style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>Last saved {lastSaved}</span>
        <div style={{ display: 'flex', gap: 12 }}>
          <button onClick={onSaveDraft} className="btn-secondary" style={{ padding: '9px 22px' }}>Save draft</button>
          {publishDisabled
            ? <button disabled style={{ background: 'var(--border)', color: 'var(--text-muted)', border: 'none', borderRadius: 999, padding: '9px 22px', fontSize: 13.5, fontWeight: 600 }}>{publishLabel}</button>
            : <button onClick={publish} className="btn-primary" style={{ padding: '9px 22px' }}>{publishLabel}</button>}
        </div>
      </div>
    </div>
  );
}
