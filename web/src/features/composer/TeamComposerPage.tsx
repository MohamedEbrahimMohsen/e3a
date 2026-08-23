import { useState } from 'react';
import { StructureTree } from '../../components/StructureTree';
import { memberSearchPool } from '../../lib/catalog';
import type { CrewMember } from '../../lib/types';
import { useToast } from '../../app/ToastContext';
import { useAuth } from '../../app/AuthContext';
import { ComposerShell } from './ComposerShell';

const labelStyle: React.CSSProperties = { fontSize: 13, fontWeight: 600, color: 'var(--text-soft)' };

export function TeamComposerPage() {
  const { login } = useAuth();
  const { showToast } = useToast();
  const [memberQuery, setMemberQuery] = useState('');
  const [lastSaved, setLastSaved] = useState('2 minutes ago');
  const [crew, setCrew] = useState<CrewMember[]>([
    { emoji: '💳', name: 'payments-engineer', pinnedVersion: 'v1.0.0' },
    { emoji: '⚛️', name: 'react-frontend', pinnedVersion: 'v2.4.1' },
  ]);

  const trimmedQuery = memberQuery.trim().toLowerCase();
  const searchResults = trimmedQuery.length > 0
    ? memberSearchPool.filter(candidate => candidate.name.includes(trimmedQuery))
    : memberSearchPool.slice(0, 2);

  const addMember = (emoji: string, name: string, version: string) => {
    setCrew([...crew, { emoji, name, pinnedVersion: version }]);
    showToast(`Added ${name}`);
  };

  return (
    <ComposerShell title="New team" lastSaved={lastSaved} onSaveDraft={() => setLastSaved('just now')}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr', flex: 1 }}>
        <div style={{ padding: '36px 40px', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: 22 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Team name</label>
            <input defaultValue="Fintech Launch Crew" className="input-field" />
            <span className="mono" style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>slug: e3a-{login}-fintech-launch-crew</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Description</label>
            <textarea defaultValue="Everything a fintech MVP needs: payments backend, compliance-aware reviews and a React frontend." className="input-field" style={{ minHeight: 72, lineHeight: 1.5, resize: 'vertical' }} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Tags</label>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '9px 12px', display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
              <span className="tag-chip" style={{ fontSize: 12, color: 'var(--text)', background: 'var(--border)', padding: '3px 10px', cursor: 'pointer' }}>fintech ×</span>
              <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>Add tag…</span>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start', background: 'rgba(34,211,238,0.05)', border: '1px solid rgba(34,211,238,0.18)', borderRadius: 12, padding: '14px 16px', fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
            <span style={{ color: 'var(--accent)' }}>ⓘ</span>
            <span>Publishing snapshots each member at its pinned version. To ship newer members later, publish a new team version.</span>
          </div>
        </div>
        <div style={{ padding: '36px 40px', display: 'flex', flexDirection: 'column', gap: 22, background: 'var(--bg-panel)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label style={labelStyle}>Add members</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '4px 14px' }}>
              <span style={{ color: 'var(--text-muted)' }}>⌕</span>
              <input value={memberQuery} onChange={event => setMemberQuery(event.target.value)} placeholder="Search published engineers…" style={{ flex: 1, background: 'transparent', border: 'none', color: 'var(--text)', fontSize: 13.5, padding: '8px 0' }} />
            </div>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
              {searchResults.map(candidate => {
                const added = crew.some(member => member.name === candidate.name);
                return (
                  <div key={candidate.name} className="hover-row" style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '11px 14px', borderBottom: '1px solid var(--surface-elevated)' }}>
                    <span style={{ fontSize: 16 }}>{candidate.emoji}</span>
                    <span className="mono" style={{ fontSize: 12.5 }}>{candidate.name}</span>
                    <span style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>@{candidate.author}</span>
                    <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span onClick={() => showToast('Version picker is stubbed in this prototype')} className="mono hover-border" style={{ fontSize: 11, color: 'var(--text-secondary)', background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 6, padding: '3px 9px', cursor: 'pointer' }}>{candidate.version} ▾</span>
                      {added
                        ? <span style={{ fontSize: 12, color: 'var(--success)', fontWeight: 600, padding: '4px 8px' }}>Added ✓</span>
                        : <button onClick={() => addMember(candidate.emoji, candidate.name, candidate.version)} className="btn-primary" style={{ borderRadius: 6, padding: '4px 12px', fontSize: 12 }}>Add</button>}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label style={labelStyle}>Members <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>— drag to order</span></label>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {crew.map((member, index) => (
                <div key={member.name} className="hover-border fade-in" style={{ display: 'flex', alignItems: 'center', gap: 12, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '12px 14px' }}>
                  <span style={{ color: 'var(--text-muted)', cursor: 'grab', letterSpacing: 2 }}>⠿</span>
                  <span className="mono" style={{ fontSize: 11, color: 'var(--text-muted)' }}>{String(index + 1).padStart(2, '0')}</span>
                  <span style={{ fontSize: 16 }}>{member.emoji}</span>
                  <span className="mono" style={{ fontSize: 12.5 }}>{member.name}</span>
                  <span className="version-badge" style={{ marginLeft: 'auto' }}>{member.pinnedVersion}</span>
                  <span onClick={() => setCrew(crew.filter((_, otherIndex) => otherIndex !== index))} className="link-danger-hover" style={{ color: 'var(--text-muted)' }}>×</span>
                </div>
              ))}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label style={labelStyle}>Structure preview</label>
            <StructureTree fontSize={12} entries={[
              { label: '.claude-plugin/', muted: true },
              { label: <><span style={{ color: 'var(--text)' }}>plugin.json</span> <span style={{ color: 'var(--text-muted)' }}>— team manifest, {crew.length} pinned members</span></>, indent: true },
              { label: 'agents/', muted: true },
              { label: crew.map(member => `${member.name}.md`).join(' · ') || '—', indent: true },
            ]} />
          </div>
        </div>
      </div>
    </ComposerShell>
  );
}
