import { Link } from 'react-router-dom';
import { config } from '../lib/config';

export function Footer() {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '28px 48px', borderTop: '1px solid var(--border)', fontSize: 13, color: 'var(--text-muted)', marginTop: 'auto' }}>
      <span className="mono">e3a — engineer as an agent</span>
      <div style={{ display: 'flex', gap: 24 }}>
        <Link to="/how" className="link-quiet" style={{ color: 'var(--text-muted)' }}>Plugin spec</Link>
        <a href={config.githubOrgUrl} target="_blank" rel="noreferrer" style={{ color: 'var(--text-muted)' }}>GitHub</a>
        <Link to="/terms" className="link-quiet" style={{ color: 'var(--text-muted)' }}>Terms</Link>
      </div>
    </div>
  );
}
