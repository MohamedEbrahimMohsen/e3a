import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';
import { clearAuthFragment, parseAuthFragment } from '../../lib/authFragment';
import { gitHubLoginUrl } from '../../lib/authApi';
import { messageForApiError, messageForErrorCode } from '../../lib/errorMessages';

export function AuthCallbackPage() {
  const navigate = useNavigate();
  const { completeSignIn } = useAuth();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const handledRef = useRef(false);

  useEffect(() => {
    if (handledRef.current) {
      return;
    }
    handledRef.current = true;

    const fragment = parseAuthFragment(window.location.hash);
    clearAuthFragment();

    if (fragment.token) {
      completeSignIn(fragment.token)
        .then(() => navigate('/workspace', { replace: true }))
        .catch(error => setErrorMessage(messageForApiError(error)));
      return;
    }

    setErrorMessage(messageForErrorCode(fragment.errorCode));
  }, [completeSignIn, navigate]);

  if (errorMessage === null) {
    return <div className="page" style={{ alignItems: 'center', color: 'var(--text-muted)', fontSize: 13.5 }}>Completing sign-in…</div>;
  }

  return (
    <div className="page fade-in" style={{ alignItems: 'center', justifyContent: 'center', flex: 1 }}>
      <div className="card" style={{ padding: '36px 40px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, textAlign: 'center', maxWidth: 420 }}>
        <span style={{ fontSize: 18, fontWeight: 700 }}>Sign-in failed</span>
        <span style={{ fontSize: 13.5, color: 'var(--text-secondary)', lineHeight: 1.6 }}>{errorMessage}</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginTop: 6 }}>
          <a className="btn-primary" href={gitHubLoginUrl()} style={{ padding: '9px 22px', fontSize: 13.5 }}>Try again</a>
          <Link to="/" className="link-quiet" style={{ fontSize: 13, color: 'var(--text-secondary)' }}>Back to home</Link>
        </div>
      </div>
    </div>
  );
}
