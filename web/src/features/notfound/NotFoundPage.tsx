import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="fade-in" style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 14, textAlign: 'center', padding: '96px 48px' }}>
      <span className="mono gradient-text" style={{ fontSize: 52, fontWeight: 600 }}>404</span>
      <span style={{ fontSize: 16, fontWeight: 700 }}>This page doesn't exist</span>
      <span style={{ fontSize: 13.5, color: 'var(--text-secondary)', maxWidth: 300, lineHeight: 1.6 }}>It may have been unpublished, or the URL has a typo.</span>
      <Link to="/catalog" className="link-violet" style={{ fontSize: 13.5, marginTop: 6 }}>← Back to catalog</Link>
    </div>
  );
}
