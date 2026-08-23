import { useParams } from 'react-router-dom';
import { InstallBlock } from '../../components/InstallBlock';
import { MetaPanel } from '../../components/MetaPanel';
import { StructureTree } from '../../components/StructureTree';
import { VersionHistory } from '../../components/VersionHistory';
import { engineerMeta, engineerVersions, findByName } from '../../lib/catalog';
import { installCommand } from '../../lib/config';
import { NotFoundPage } from '../notfound/NotFoundPage';
import { DetailHeader } from './DetailHeader';

export function EngineerDetailPage() {
  const { name } = useParams();
  const item = name ? findByName(name) : undefined;
  if (!item || item.team) {
    return <NotFoundPage />;
  }

  const skillA = item.tags[0] ? `${item.tags[0]}-feature` : 'core-skill';
  const skillB = item.tags[1] ? `${item.tags[1]}-review` : 'house-style';

  return (
    <div className="page fade-in" style={{ gap: 28 }}>
      <DetailHeader item={item} />
      <InstallBlock line2={installCommand(item.author, item.name)} />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 340px', gap: 32, alignItems: 'start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
          <div style={{ fontSize: 14.5, lineHeight: 1.7, color: 'var(--text-soft)', display: 'flex', flexDirection: 'column', gap: 14 }}>
            <p>{item.description} It follows one consistent house style across every feature it touches.</p>
            <h3 style={{ marginTop: 10, fontSize: 16, fontWeight: 700 }}>What this engineer does</h3>
            <ul style={{ margin: 0, paddingLeft: 20, color: 'var(--text-secondary)', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <li>Scaffolds features end to end in its area of expertise</li>
              <li>Follows the persona's rules on every change it makes</li>
              <li>Ships with its skills versioned and pinned together</li>
              <li>Works alongside other engineers in a team install</li>
            </ul>
            <p style={{ color: 'var(--text-secondary)' }}>Persona and skills are versioned together — pin a version to freeze behavior across your team.</p>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h3 style={{ fontSize: 16, fontWeight: 700 }}>Plugin structure</h3>
            <StructureTree entries={[
              { label: '.claude-plugin/', muted: true },
              { label: <span style={{ color: 'var(--text)' }}>plugin.json</span>, indent: true },
              { label: 'agents/', muted: true },
              { label: `${item.name}.md`, indent: true },
              { label: 'skills/', muted: true },
              { label: <>{skillA}/ <span style={{ color: 'var(--text)' }}>SKILL.md</span></>, indent: true },
              { label: <>{skillB}/ <span style={{ color: 'var(--text)' }}>SKILL.md</span></>, indent: true },
              { label: 'commands/', muted: true },
              { label: 'spec-implement.md', indent: true },
            ]} />
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <MetaPanel meta={engineerMeta} tags={item.tags} />
          <VersionHistory pluginName={item.name} versions={engineerVersions} />
        </div>
      </div>
    </div>
  );
}
