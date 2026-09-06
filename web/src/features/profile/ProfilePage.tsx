import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { EngineerCard } from '../../components/EngineerCard';
import { getCreatorProfile, type CreatorProfile } from '../../lib/api';
import { messageForApiError } from '../../lib/errorMessages';
import { initialsFor } from '../../lib/initials';
import type { CatalogItem } from '../../lib/types';
import { formatTotalInstalls, joinedLabel, toEngineerItem, toTeamItem } from './profileItems';

const tabs = ['Engineers', 'Teams'] as const;
type ProfileTab = (typeof tabs)[number];

export function ProfilePage() {
  const { login = '' } = useParams();
  const [tab, setTab] = useState<ProfileTab>('Engineers');
  const [profile, setProfile] = useState<CreatorProfile | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let cancelled = false;
    getCreatorProfile(login)
      .then(result => { if (!cancelled) { setProfile(result); setErrorMessage(null); } })
      .catch(error => { if (!cancelled) { setErrorMessage(messageForApiError(error)); } });
    return () => { cancelled = true; };
  }, [login, reloadToken]);

  const engineers = profile?.engineers ?? [];
  const teams = profile?.teams ?? [];
  const items: CatalogItem[] = tab === 'Engineers' ? engineers.map(toEngineerItem) : teams.map(toTeamItem);
  const counts: Record<ProfileTab, number> = { Engineers: engineers.length, Teams: teams.length };
  const loading = profile === null && errorMessage === null;
  const joined = joinedLabel(profile?.createdAt);
  const totalInstalls = formatTotalInstalls(profile?.totalInstalls ?? 0);
  const displayedLogin = profile?.gitHubLogin ?? login;

  return (
    <div className="page fade-in" style={{ paddingTop: 48, gap: 32 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 22 }}>
        {profile?.avatarUrl
          ? <img src={profile.avatarUrl} alt="" width={76} height={76} style={{ borderRadius: '50%', border: '1px solid var(--border)' }} />
          : <div style={{ width: 76, height: 76, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#1d1d23)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26, fontWeight: 700, color: 'var(--text-secondary)' }}>{initialsFor(profile?.displayName ?? login)}</div>}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <h1 style={{ fontSize: 26, fontWeight: 700 }}>{profile?.displayName ?? `@${displayedLogin}`}</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16, fontSize: 13.5, color: 'var(--text-secondary)' }}>
            <a href={`https://github.com/${encodeURIComponent(displayedLogin)}`} target="_blank" rel="noreferrer" style={{ fontSize: 13.5 }}>github.com/{displayedLogin} ↗</a>
            {joined && <><span style={{ color: 'var(--text-muted)' }}>·</span><span>Joined {joined}</span></>}
            {profile !== null && <><span style={{ color: 'var(--text-muted)' }}>·</span><span className="mono" style={{ fontSize: 12.5 }}>{totalInstalls} total installs</span></>}
          </div>
        </div>
      </div>
      <div style={{ display: 'flex', gap: 4, borderBottom: '1px solid var(--border)' }}>
        {tabs.map(label => (
          <button key={label} type="button" onClick={() => setTab(label)} aria-current={tab === label ? 'true' : undefined} className="link-quiet" style={{ fontSize: 14, fontWeight: 600, color: tab === label ? 'var(--text)' : 'var(--text-muted)', padding: '10px 18px', borderBottom: `2px solid ${tab === label ? 'var(--primary)' : 'transparent'}`, background: 'none', border: 'none', borderRadius: 0, fontFamily: 'inherit', cursor: 'pointer', transition: 'all 0.15s ease' }}>
            {label} <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>{counts[label]}</span>
          </button>
        ))}
      </div>
      {errorMessage ? (
        <div style={{ padding: 48, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, textAlign: 'center' }}>
          <span style={{ fontSize: 15, fontWeight: 700 }}>Could not load this profile</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>{errorMessage}</span>
          <button type="button" onClick={() => { setErrorMessage(null); setReloadToken(reloadToken + 1); }} className="btn-secondary" style={{ padding: '7px 16px', fontSize: 12.5 }}>Retry</button>
        </div>
      ) : loading ? (
        <div style={{ padding: 48, textAlign: 'center', color: 'var(--text-muted)', fontSize: 13.5 }}>Loading…</div>
      ) : items.length > 0 ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 20 }}>
          {items.map(item => <EngineerCard key={item.name} item={item} />)}
        </div>
      ) : (
        <div style={{ padding: 48, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10, textAlign: 'center' }}>
          <span style={{ fontSize: 15, fontWeight: 700 }}>Nothing here yet</span>
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>@{displayedLogin} hasn't published any {tab.toLowerCase()} yet.</span>
        </div>
      )}
    </div>
  );
}
