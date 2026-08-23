import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { StructureTree } from '../../components/StructureTree';
import { faqEntries, scanCategories } from '../../lib/catalog';

export function HowItWorksPage() {
  const navigate = useNavigate();
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  const steps = [
    { number: '1', title: 'Browse', detail: 'Find an engineer or a whole team in the catalog. Read the persona, skills and scan status before you commit.', target: '/catalog' },
    { number: '2', title: 'Copy the command', detail: 'Two lines: add the e3a marketplace once, then install the plugin. Pin a version if you need it frozen.', target: '/e/backend-engineer' },
    { number: '3', title: 'Claude Code has your team', detail: 'The persona, skills and commands are live in your next session. Update or remove them like any plugin.', target: '/catalog?seg=Teams' },
  ];

  return (
    <div className="fade-in" style={{ maxWidth: 1000, width: '100%', margin: '0 auto', padding: '64px 48px 72px', display: 'flex', flexDirection: 'column', gap: 64 }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14, alignItems: 'center', textAlign: 'center' }}>
        <h1 style={{ fontSize: 38, fontWeight: 800, letterSpacing: '-0.02em' }}>How it works</h1>
        <p style={{ fontSize: 16, color: 'var(--text-secondary)', maxWidth: 520, lineHeight: 1.6 }}>No accounts to install, nothing to configure. Engineers are plain Claude Code plugins served from a public marketplace.</p>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 20 }}>
        {steps.map(step => (
          <div key={step.number} onClick={() => navigate(step.target)} className="card hover-lift-violet" style={{ borderRadius: 16, padding: 26, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="mono" style={{ width: 40, height: 40, borderRadius: '50%', background: 'rgba(139,92,246,0.12)', border: '1px solid rgba(139,92,246,0.3)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 15, fontWeight: 600, color: 'var(--primary-tint)' }}>{step.number}</div>
            <h3 style={{ fontSize: 17, fontWeight: 700 }}>{step.title}</h3>
            <p style={{ fontSize: 13.5, color: 'var(--text-secondary)', lineHeight: 1.6 }}>{step.detail}</p>
          </div>
        ))}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 40, alignItems: 'center' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <h2 style={{ fontSize: 24, fontWeight: 700, letterSpacing: '-0.01em' }}>What's inside a plugin</h2>
          <p style={{ fontSize: 14.5, color: 'var(--text-secondary)', lineHeight: 1.7 }}>An engineer is a persona file plus its skills, laid out in the standard Claude Code plugin structure. A team is a manifest of engineers, each pinned to an exact version. Everything is plain markdown and JSON — inspect any plugin before you install it.</p>
          <Link to="/terms" className="link-violet" style={{ fontSize: 14 }}>Read the plugin spec →</Link>
        </div>
        <StructureTree entries={[
          { label: '.claude-plugin/', muted: true },
          { label: <span style={{ color: 'var(--text)' }}>plugin.json</span>, indent: true },
          { label: 'agents/', muted: true },
          { label: 'backend-engineer.md', indent: true },
          { label: 'skills/', muted: true },
          { label: <>dotnet-feature/ <span style={{ color: 'var(--text)' }}>SKILL.md</span></>, indent: true },
          { label: 'commands/', muted: true },
          { label: 'spec-implement.md', indent: true },
        ]} />
      </div>
      <div className="card" style={{ borderRadius: 16, padding: 32, display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ color: 'var(--success)', fontSize: 18 }}>✓</span>
          <h2 style={{ fontSize: 20, fontWeight: 700 }}>Every publish is scanned</h2>
        </div>
        <p style={{ fontSize: 14, color: 'var(--text-secondary)', lineHeight: 1.6, maxWidth: 640 }}>Before anything goes live, every file is checked line by line. Publishes that trip a rule are rejected with an exact report — file, line and finding.</p>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          {scanCategories.map(category => (
            <span key={category.code} onClick={() => navigate('/workspace/publish?mode=rejected')} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--text-soft)', background: 'var(--surface-elevated)', border: '1px solid var(--border)', borderRadius: 999, padding: '7px 16px', cursor: 'pointer', transition: 'all 0.15s ease' }}>
              <span className="mono" style={{ fontSize: 10.5, color: 'var(--danger)' }}>{category.code}</span>{category.name}
            </span>
          ))}
        </div>
        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>Click a category to see an example scan report.</span>
      </div>
      <div>
        <h2 style={{ margin: '0 0 18px', fontSize: 24, fontWeight: 700, letterSpacing: '-0.01em' }}>FAQ</h2>
        {faqEntries.map((faq, index) => (
          <div key={index} onClick={() => setOpenFaq(openFaq === index ? null : index)} className="hover-bg-deep" style={{ borderTop: '1px solid var(--border)', padding: '18px 4px', display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 16 }}>
              <span style={{ fontSize: 15, fontWeight: 600 }}>{faq.question}</span>
              <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>{openFaq === index ? '▴' : '▾'}</span>
            </div>
            {openFaq === index && <p className="fade-in" style={{ fontSize: 14, color: 'var(--text-secondary)', lineHeight: 1.65, maxWidth: 760 }}>{faq.answer}</p>}
          </div>
        ))}
      </div>
    </div>
  );
}
