import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../app/AuthContext';
import { useToast } from '../app/ToastContext';
import { config } from '../lib/config';

function navLinkStyle(isActive: boolean): React.CSSProperties {
  return { color: isActive ? 'var(--text)' : 'var(--text-secondary)', fontSize: 14, transition: 'color 0.15s ease' };
}

export function NavBar() {
  const { signedIn, login, signIn } = useAuth();
  const { showToast } = useToast();
  const navigate = useNavigate();

  const handleSignIn = () => {
    signIn();
    navigate('/workspace');
    showToast(`Signed in as @${login}`);
  };

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
        {signedIn
          ? <div onClick={() => navigate(`/u/${login}`)} className="hover-border-violet" style={{ width: 32, height: 32, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#1d1d23)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, color: 'var(--text-secondary)', cursor: 'pointer' }}>MD</div>
          : <button onClick={handleSignIn} className="btn-primary" style={{ padding: '8px 18px', fontSize: 13.5 }}>Sign in with GitHub</button>}
      </div>
    </div>
  );
}
