import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { EngineerCard } from '../../components/EngineerCard';
import { engineers, teams } from '../../lib/catalog';

const tabs = ['Engineers', 'Teams'] as const;
type ProfileTab = (typeof tabs)[number];

export function ProfilePage() {
  const { login = '' } = useParams();
  const [tab, setTab] = useState<ProfileTab>('Engineers');

  const ownEngineers = engineers.filter(item => item.author === login);
  const ownTeams = teams.filter(item => item.author === login);
  const items = tab === 'Engineers' ? ownEngineers : ownTeams;
  const totalInstalls = [...ownEngineers, ...ownTeams].reduce((total, item) => total + item.installs, 0).toLocaleString('en-US');
  const initials = login.split(/[^a-z0-9]+/i).map(word => word[0] ?? '').join('').slice(0, 2).toUpperCase();
  const counts: Record<ProfileTab, number> = { Engineers: ownEngineers.length, Teams: ownTeams.length };

  return (
    <div className="page fade-in" style={{ paddingTop: 48, gap: 32 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 22 }}>
        <div style={{ width: 76, height: 76, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#1d1d23)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26, fontWeight: 700, color: 'var(--text-secondary)' }}>{initials}</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <h1 style={{ fontSize: 26, fontWeight: 700 }}>@{login}</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16, fontSize: 13.5, color: 'var(--text-secondary)' }}>
            <a href={`https://github.com/${login}`} target="_blank" rel="noreferrer" style={{ fontSize: 13.5 }}>github.com/{login} ↗</a>
            <span style={{ color: 'var(--text-muted)' }}>·</span><span>Joined March 2026</span>
            <span style={{ color: 'var(--text-muted)' }}>·</span><span className="mono" style={{ fontSize: 12.5 }}>{totalInstalls} total installs</span>
          </div>
        </div>
      </div>
      <div style={{ display: 'flex', gap: 4, borderBottom: '1px solid var(--border)' }}>
        {tabs.map(label => (
          <span key={label} onClick={() => setTab(label)} className="link-quiet" style={{ fontSize: 14, fontWeight: 600, color: tab === label ? 'var(--text)' : 'var(--text-muted)', padding: '10px 18px', borderBottom: `2px solid ${tab === label ? 'var(--primary)' : 'transparent'}`, cursor: 'pointer', transition: 'all 0.15s ease' }}>
            {label} <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>{counts[label]}</span>
          </span>
        ))}
      </div>
      {items.length > 0 ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 20 }}>
          {items.map(item => <EngineerCard key={item.name} item={item} />)}
        </div>
      ) : (
        <div style={{ padding: 48, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10, textAlign: 'center' }}>
          <span style={{ fontSize: 15, fontWeight: 700 }}>Nothing here yet</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>@{login} hasn't published any {tab.toLowerCase()}.</span>
        </div>
      )}
    </div>
  );
}
