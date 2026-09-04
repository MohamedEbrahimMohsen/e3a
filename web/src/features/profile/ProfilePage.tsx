import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useAuth } from '../../app/AuthContext';
import { EngineerCard } from '../../components/EngineerCard';
import { emojiFor } from '../../lib/api';
import { messageForApiError } from '../../lib/errorMessages';
import { initialsFor } from '../../lib/initials';
import type { CatalogItem } from '../../lib/types';
import { listMyEngineers, listMyTeams, type Engineer, type Team } from '../../lib/workspaceApi';

const tabs = ['Engineers', 'Teams'] as const;
type ProfileTab = (typeof tabs)[number];

const PUBLISHED_STATUS = 'Published';

function toEngineerItem(engineer: Engineer): CatalogItem {
  return { emoji: emojiFor(engineer.slug), name: engineer.slug, description: engineer.description ?? '', tags: engineer.tags, installs: engineer.installCount };
}

function toTeamItem(team: Team): CatalogItem {
  return { emoji: emojiFor(team.slug), name: team.slug, description: team.description ?? '', tags: team.tags, installs: 0, team: true };
}

function joinedLabel(createdAt: string | undefined): string {
  if (!createdAt) {
    return '';
  }
  const joined = new Date(createdAt);
  return Number.isNaN(joined.getTime()) ? '' : joined.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
}

export function ProfilePage() {
  const { login = '' } = useParams();
  const { status, user } = useAuth();
  const [tab, setTab] = useState<ProfileTab>('Engineers');
  const [engineers, setEngineers] = useState<Engineer[] | null>(null);
  const [teams, setTeams] = useState<Team[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const isOwnProfile = status === 'signedIn' && (user?.gitHubLogin ?? '').toLowerCase() === login.toLowerCase();

  useEffect(() => {
    if (!isOwnProfile) {
      return;
    }
    let cancelled = false;
    Promise.all([listMyEngineers(), listMyTeams()])
      .then(([myEngineers, myTeams]) => {
        if (!cancelled) {
          setEngineers(myEngineers);
          setTeams(myTeams);
          setErrorMessage(null);
        }
      })
      .catch(error => { if (!cancelled) { setErrorMessage(messageForApiError(error)); } });
    return () => { cancelled = true; };
  }, [isOwnProfile, reloadToken]);

  const ownEngineers = (engineers ?? []).filter(engineer => engineer.status === PUBLISHED_STATUS);
  const ownTeams = (teams ?? []).filter(team => team.status === PUBLISHED_STATUS);
  const items: CatalogItem[] = tab === 'Engineers' ? ownEngineers.map(toEngineerItem) : ownTeams.map(toTeamItem);
  const totalInstalls = ownEngineers.reduce((total, engineer) => total + engineer.installCount, 0).toLocaleString('en-US');
  const counts: Record<ProfileTab, number> = { Engineers: ownEngineers.length, Teams: ownTeams.length };
  const joined = isOwnProfile ? joinedLabel(user?.createdAt) : '';
  const loading = isOwnProfile && engineers === null && errorMessage === null;

  return (
    <div className="page fade-in" style={{ paddingTop: 48, gap: 32 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 22 }}>
        {isOwnProfile && user?.avatarUrl
          ? <img src={user.avatarUrl} alt="" width={76} height={76} style={{ borderRadius: '50%', border: '1px solid var(--border)' }} />
          : <div style={{ width: 76, height: 76, borderRadius: '50%', background: 'linear-gradient(135deg,#3f3f46,#1d1d23)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26, fontWeight: 700, color: 'var(--text-secondary)' }}>{initialsFor(login)}</div>}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <h1 style={{ fontSize: 26, fontWeight: 700 }}>@{login}</h1>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16, fontSize: 13.5, color: 'var(--text-secondary)' }}>
            <a href={`https://github.com/${encodeURIComponent(login)}`} target="_blank" rel="noreferrer" style={{ fontSize: 13.5 }}>github.com/{login} ↗</a>
            {joined && <><span style={{ color: 'var(--text-muted)' }}>·</span><span>Joined {joined}</span></>}
            {isOwnProfile && <><span style={{ color: 'var(--text-muted)' }}>·</span><span className="mono" style={{ fontSize: 12.5 }}>{totalInstalls} total installs</span></>}
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
          <span style={{ fontSize: 13.5, color: 'var(--text-secondary)' }}>
            {isOwnProfile
              ? `You haven't published any ${tab.toLowerCase()} yet.`
              : `Public profiles aren't available yet — sign in to see your own published ${tab.toLowerCase()}.`}
          </span>
        </div>
      )}
    </div>
  );
}
