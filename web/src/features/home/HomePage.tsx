import { Link } from 'react-router-dom';
import { EngineerCard } from '../../components/EngineerCard';
import { InstallBlock } from '../../components/InstallBlock';
import { featuredEngineers, homeStats, teams } from '../../lib/catalog';
import { installCommand } from '../../lib/config';

function FeaturedRow({ title, browseTo, items }: { title: string; browseTo: string; items: typeof teams }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between' }}>
        <h2 style={{ fontSize: 20, fontWeight: 700, letterSpacing: '-0.01em' }}>{title}</h2>
        <Link to={browseTo} className="link-violet" style={{ fontSize: 13.5 }}>Browse all →</Link>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 20 }}>
        {items.map(item => <EngineerCard key={item.name} item={item} />)}
      </div>
    </div>
  );
}

export function HomePage() {
  return (
    <div className="fade-in" style={{ display: 'flex', flexDirection: 'column', flex: 1 }}>
      <div style={{ position: 'relative', padding: '104px 48px 88px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 24, overflow: 'hidden' }}>
        <div style={{ position: 'absolute', top: -180, left: '50%', transform: 'translateX(-50%)', width: 820, height: 460, background: 'radial-gradient(ellipse at center, rgba(189,52,254,0.22), rgba(100,108,255,0.10) 55%, transparent 75%)', filter: 'blur(40px)', pointerEvents: 'none' }} />
        <div className="mono" style={{ position: 'relative', fontSize: 12, color: 'var(--accent)', background: 'rgba(34,211,238,0.08)', border: '1px solid rgba(34,211,238,0.25)', borderRadius: 999, padding: '5px 14px' }}>free · open catalog · works with Claude Code</div>
        <h1 style={{ position: 'relative', fontSize: 58, fontWeight: 800, letterSpacing: '-0.03em', textAlign: 'center', lineHeight: 1.08, maxWidth: 820 }}>Hire an AI engineering team<br />in one command</h1>
        <p style={{ position: 'relative', fontSize: 17, color: 'var(--text-secondary)', textAlign: 'center', maxWidth: 560, lineHeight: 1.6 }}>Browse engineers composed by the community — skills plus a persona, packaged as a Claude Code plugin. Copy one command and they're on your project.</p>
        <div style={{ position: 'relative', width: 660, marginTop: 12 }}>
          <InstallBlock line2={installCommand('mohamed-dive', 'backend-engineer')} />
        </div>
        <div style={{ position: 'relative', display: 'flex', gap: 64, marginTop: 36 }}>
          {homeStats.map(stat => (
            <div key={stat.label} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
              <span className="mono" style={{ fontSize: 26, fontWeight: 600 }}>{stat.value}</span>
              <span style={{ fontSize: 12.5, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.1em' }}>{stat.label}</span>
            </div>
          ))}
        </div>
      </div>
      <div style={{ maxWidth: 1200, width: '100%', margin: '0 auto', padding: '24px 48px 72px', display: 'flex', flexDirection: 'column', gap: 48 }}>
        <FeaturedRow title="Featured engineers" browseTo="/catalog?seg=Engineers" items={featuredEngineers} />
        <FeaturedRow title="Featured teams" browseTo="/catalog?seg=Teams" items={teams} />
      </div>
    </div>
  );
}
