import { Outlet } from 'react-router-dom';
import { gitHubLoginUrl } from '../lib/authApi';
import { useAuth } from './AuthContext';

function SignInRequired() {
  return (
    <div className="page fade-in" style={{ alignItems: 'center', justifyContent: 'center', flex: 1 }}>
      <div className="card" style={{ padding: '36px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, textAlign: 'center', maxWidth: 380 }}>
        <span style={{ fontSize: 18, fontWeight: 700 }}>Sign in to continue</span>
        <span style={{ fontSize: 13.5, color: 'var(--text-secondary)', lineHeight: 1.6 }}>Creator tools need a GitHub account.</span>
        <a className="btn-primary" href={gitHubLoginUrl()} style={{ padding: '9px 22px', fontSize: 13.5, marginTop: 6 }}>Sign in with GitHub</a>
      </div>
    </div>
  );
}

export function RequireAuth() {
  const { status } = useAuth();

  if (status === 'loading') {
    return <div className="page" style={{ alignItems: 'center', color: 'var(--text-muted)', fontSize: 13.5 }}>Loading…</div>;
  }

  return status === 'signedIn' ? <Outlet /> : <SignInRequired />;
}
