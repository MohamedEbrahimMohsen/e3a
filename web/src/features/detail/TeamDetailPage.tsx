import { useNavigate, useParams } from 'react-router-dom';
import { InstallBlock } from '../../components/InstallBlock';
import { MetaPanel } from '../../components/MetaPanel';
import { VersionHistory } from '../../components/VersionHistory';
import { findByName, squadMembers, teamMeta, teamVersions } from '../../lib/catalog';
import { installCommand } from '../../lib/config';
import { NotFoundPage } from '../notfound/NotFoundPage';
import { DetailHeader } from './DetailHeader';

export function TeamDetailPage() {
  const { name } = useParams();
  const navigate = useNavigate();
  const item = name ? findByName(name) : undefined;
  if (!item || !item.team) {
    return <NotFoundPage />;
  }

  return (
    <div className="page fade-in" style={{ gap: 28 }}>
      <DetailHeader item={item} />
      <InstallBlock line2={installCommand(item.author ?? 'creator', item.name)} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 340px', gap: 32, alignItems: 'start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
          <p style={{ fontSize: 14.5, lineHeight: 1.7, color: 'var(--text-soft)' }}>{item.description} One install brings every member below, each frozen at the version it was published with.</p>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h3 style={{ fontSize: 16, fontWeight: 700 }}>Members</h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {squadMembers.map(member => (
                <div key={member.name} onClick={() => navigate(`/e/${member.name}`)} className="card hover-lift-violet" style={{ display: 'flex', alignItems: 'center', gap: 14, borderRadius: 12, padding: '14px 18px' }}>
                  <div style={{ width: 38, height: 38, borderRadius: 9, background: 'var(--surface-elevated)', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 19 }}>{member.emoji}</div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                    <span className="mono" style={{ fontSize: 13.5, fontWeight: 600 }}>{member.name}</span>
                    <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>@{member.author}</span>
                  </div>
                  <span className="version-badge" style={{ marginLeft: 'auto' }}>pinned {member.pinnedVersion}</span>
                  <span style={{ fontSize: 13, color: 'var(--primary)' }}>View →</span>
                </div>
              ))}
            </div>
            <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start', background: 'rgba(34,211,238,0.05)', border: '1px solid rgba(34,211,238,0.18)', borderRadius: 12, padding: '14px 18px', fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
              <span style={{ color: 'var(--accent)' }}>ⓘ</span>
              <span>Teams are immutable snapshots. Each member is pinned to the exact version it was published with — later engineer releases never change a team you already installed.</span>
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <MetaPanel meta={teamMeta} tags={item.tags} />
          <VersionHistory pluginName={`${item.name}-team`} versions={teamVersions} />
        </div>
      </div>
    </div>
  );
}
