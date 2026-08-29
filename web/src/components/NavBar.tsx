import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../app/AuthContext';
import { useToast } from '../app/ToastContext';
import { gitHubLoginUrl } from '../lib/authApi';
import { config } from '../lib/config';
import { initialsFor } from '../lib/initials';

const AVATAR_SIZE = 32;

function navLinkStyle(isActive: boolean): React.CSSProperties {
  return { color: isActive ? 'var(--text)' : 'var(--text-secondary)', fontSize: 14, transition: 'color 0.15s ease' };
}

export function NavBar() {
  const { status, signedIn, login, user, signOut } = useAuth();
  const { showToast } = useToast();
  const navigate = useNavigate();

  const handleSignOut = () => {
    signOut();
    showToast('Signed out');
    navigate('/');
  };

  const openProfile = () => navigate(`/u/${login}`);

  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 48px', height: 64, borderBottom: '1px solid var(--border)', position: 'sticky', top: 0, background: 'rgba(11,11,15,0.85)', backdropFilter: 'blur(12px)', zIndex: 20 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 36 }}>
        <NavLink to="/" className="gradient-text" style={{ fontWeight: 800, fontSize: 19, letterSpacing: '-0.02em' }}>e3a</NavLink>
        <div style={{ display: 'flex', gap: 26 }}>
          <NavLink to="/catalog" className="link-quiet" style={({ isActive }) => navLinkStyle(isActive)}>Catalog</NavLink>
          <NavLink to="/how" className="link-quiet" style={({ isActive }) => navLinkStyle(isActive)}>How it works</NavLink>
          {signedIn && <NavLink to="/workspace" className="link-quiet" style={({ isActive }) => navLinkStyle(isActive)}>My workspace</NavLink>}
        </div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
        <a href={config.githubOrgUrl} target="_blank" rel="noreferrer" style={{ fontSize: 14, color: 'var(--text-secondary)' }}>GitHub ↗</a>
        {status === 'loading' && <span style={{ width: AVATAR_SIZE, height: AVATAR_SIZE }} />}
        {status === 'signedOut' && <a className="btn-primary" style={{ padding: '8px 18px', fontSize: 13.5 }} href={gitHubLoginUrl()}>Sign in with GitHub</a>}
        {status === 'signedIn' && (
          <>
            <span onClick={handleSignOut} className="link-quiet" style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>Sign out</span>
            {user?.avatarUrl
              ? <img onClick={openProfile} src={user.avatarUrl} alt="" width={AVATAR_SIZE} height={AVATAR_SIZE} className="hover-border-violet" style={{ borderRadius: '50%', border: '1px solid var(--border)', cursor: 'pointer' }} />
              : <div onClick={openProfile} className="hover-border-violet" style={{ width: AVATAR_SIZE, height: AVATAR_SIZE, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#1d1d23)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: 'var(--text-secondary)', cursor: 'pointer' }}>{initialsFor(user?.displayName ?? login)}</div>}
          </>
        )}
      </div>
    </div>
  );
}
