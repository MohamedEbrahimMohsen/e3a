import { useState } from 'react';
import { ModalOverlay } from '../../components/ModalOverlay';
import { StructureTree } from '../../components/StructureTree';
import { pickableSkills } from '../../lib/catalog';
import type { DraftSkill } from '../../lib/types';
import { useToast } from '../../app/ToastContext';
import { useAuth } from '../../app/AuthContext';
import { ComposerShell } from './ComposerShell';

type ComposerModal = 'pick' | 'github' | 'upload' | null;

const labelStyle: React.CSSProperties = { fontSize: 13, fontWeight: 600, color: 'var(--text-soft)' };

export function EngineerComposerPage() {
  const { login } = useAuth();
  const { showToast } = useToast();
  const [draftName, setDraftName] = useState('Payments Engineer');
  const [personaPreview, setPersonaPreview] = useState(false);
  const [modal, setModal] = useState<ComposerModal>(null);
  const [pickIndex, setPickIndex] = useState(0);
  const [lastSaved, setLastSaved] = useState('2 minutes ago');
  const [skills, setSkills] = useState<DraftSkill[]>([
    { name: 'gateway-integrations', source: 'catalog', size: '24 KB' },
    { name: 'webhook-handling', source: 'github', size: '11 KB' },
  ]);

  const draftSlug = `e3a-${login}-${(draftName || 'unnamed').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')}`;
  const agentFile = `${draftSlug.replace(`e3a-${login}-`, '')}.md`;

  const addSkill = (name: string, source: DraftSkill['source'], size: string) => {
    if (!skills.some(skill => skill.name === name)) {
      setSkills([...skills, { name, source, size }]);
    }
    setModal(null);
    showToast(`Skill added — ${name}`);
  };

  const removeSkill = (index: number) => {
    setSkills(skills.filter((_, otherIndex) => otherIndex !== index));
    showToast('Skill removed');
  };

  const skillAddButton = (label: string, icon: string, onClick: () => void) => (
    <button onClick={onClick} className="hover-border-violet" style={{ flex: 1, background: 'var(--surface)', color: 'var(--text-soft)', border: '1px solid var(--border)', borderRadius: 10, padding: '12px 10px', fontSize: 12.5, fontWeight: 600, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
      <span style={{ fontSize: 16, color: 'var(--primary)' }}>{icon}</span>{label}
    </button>
  );

  return (
    <ComposerShell title="New engineer" lastSaved={lastSaved} onSaveDraft={() => setLastSaved('just now')}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', flex: 1 }}>
        <div style={{ padding: '36px 40px', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: 22 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Name</label>
            <input value={draftName} onChange={event => setDraftName(event.target.value)} className="input-field" />
            <span className="mono" style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>slug: {draftSlug}</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Description</label>
            <textarea defaultValue="Integrates payment gateways with idempotent retries, webhook handling and reconciliation jobs." className="input-field" style={{ minHeight: 52, lineHeight: 1.5, resize: 'vertical' }} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Tags</label>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '9px 12px', display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
              {['payments', 'dotnet'].map(tag => (
                <span key={tag} className="tag-chip" style={{ fontSize: 12, color: 'var(--text)', background: 'var(--border)', padding: '3px 10px', cursor: 'pointer' }}>{tag} ×</span>
              ))}
              <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>Add tag…</span>
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, flex: 1 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <label style={labelStyle}>Persona</label>
              <div style={{ display: 'flex', background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 8, padding: 2 }}>
                {(['Write', 'Preview'] as const).map(mode => {
                  const active = (mode === 'Preview') === personaPreview;
                  return <span key={mode} onClick={() => setPersonaPreview(mode === 'Preview')} style={{ fontSize: 12, fontWeight: 600, padding: '4px 12px', borderRadius: 6, background: active ? 'var(--border)' : 'transparent', color: active ? 'var(--text)' : 'var(--text-muted)', cursor: 'pointer', transition: 'all 0.15s ease' }}>{mode}</span>;
                })}
              </div>
            </div>
            {!personaPreview ? (
              <div className="mono" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, flex: 1, minHeight: 220, fontSize: 12.5, lineHeight: 1.9, color: 'var(--text-soft)' }}>
                <div style={{ color: 'var(--primary)' }}># Payments Engineer</div>
                <br />
                <div>You are a senior payments engineer. You treat every write</div>
                <div>as retry-able: idempotency keys on all mutations, exactly-once</div>
                <div>semantics via the outbox pattern.</div>
                <br />
                <div style={{ color: 'var(--primary)' }}>## Rules</div>
                <div>- Never log full card numbers or tokens</div>
                <div>- Webhooks are verified before processing<span style={{ display: 'inline-block', width: 2, height: 14, background: 'var(--primary)', verticalAlign: 'middle', marginLeft: 1 }} /></div>
              </div>
            ) : (
              <div className="fade-in" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, flex: 1, minHeight: 220, fontSize: 13.5, lineHeight: 1.7, color: 'var(--text-soft)', display: 'flex', flexDirection: 'column', gap: 10 }}>
                <span style={{ fontSize: 18, fontWeight: 700, color: 'var(--text)' }}>Payments Engineer</span>
                <p>You are a senior payments engineer. You treat every write as retry-able: idempotency keys on all mutations, exactly-once semantics via the outbox pattern.</p>
                <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--text)', marginTop: 6 }}>Rules</span>
                <ul style={{ margin: 0, paddingLeft: 20, color: 'var(--text-secondary)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                  <li>Never log full card numbers or tokens</li>
                  <li>Webhooks are verified before processing</li>
                </ul>
              </div>
            )}
          </div>
        </div>
        <div style={{ padding: '36px 40px', display: 'flex', flexDirection: 'column', gap: 22, background: 'var(--bg-panel)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <label style={labelStyle}>Skills</label>
            <div style={{ display: 'flex', gap: 10 }}>
              {skillAddButton('Pick from catalog', '⊞', () => setModal('pick'))}
              {skillAddButton('Add from GitHub URL', '⎇', () => setModal('github'))}
              {skillAddButton('Upload', '↑', () => setModal('upload'))}
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {skills.map((skill, index) => (
                <div key={skill.name} className="hover-border fade-in" style={{ display: 'flex', alignItems: 'center', gap: 12, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '12px 16px' }}>
                  <span className="mono" style={{ fontSize: 12.5 }}>{skill.name}</span>
                  <span style={{ fontSize: 11, color: 'var(--text-muted)', background: 'var(--surface-elevated)', borderRadius: 6, padding: '2px 8px' }}>{skill.source}</span>
                  <span className="mono" style={{ marginLeft: 'auto', fontSize: 11, color: 'var(--text-muted)' }}>{skill.size}</span>
                  <span onClick={() => removeSkill(index)} className="link-danger-hover" style={{ color: 'var(--text-muted)' }}>×</span>
                </div>
              ))}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label style={labelStyle}>Structure preview</label>
            <StructureTree fontSize={12} entries={[
              { label: '.claude-plugin/', muted: true },
              { label: <span style={{ color: 'var(--text)' }}>plugin.json</span>, indent: true },
              { label: 'agents/', muted: true },
              { label: agentFile, indent: true },
              { label: 'skills/', muted: true },
              ...skills.map(skill => ({ label: <>{skill.name}/ <span style={{ color: 'var(--text)' }}>SKILL.md</span></>, indent: true })),
            ]} />
          </div>
        </div>
      </div>

      {modal === 'pick' && (
        <ModalOverlay onClose={() => setModal(null)}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 15, fontWeight: 700 }}>Pick from catalog</span>
            <span onClick={() => setModal(null)} className="link-quiet" style={{ color: 'var(--text-muted)', cursor: 'pointer' }}>✕</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 9, padding: '9px 12px', fontSize: 13, color: 'var(--text-muted)' }}><span>⌕</span>Search published skills…</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {pickableSkills.map((skill, index) => {
              const selected = pickIndex === index;
              return (
                <div key={skill.name} onClick={() => setPickIndex(index)} className="hover-row" style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px', borderRadius: 9, background: selected ? 'var(--surface-elevated)' : 'transparent', border: `1px solid ${selected ? 'rgba(139,92,246,0.4)' : 'transparent'}`, cursor: 'pointer', transition: 'all 0.15s ease' }}>
                  <span className="mono" style={{ fontSize: 12.5 }}>{skill.name}</span>
                  <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>@{skill.author}</span>
                  {selected && <span style={{ marginLeft: 'auto', color: 'var(--primary)', fontSize: 13 }}>✓</span>}
                </div>
              );
            })}
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button onClick={() => setModal(null)} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13 }}>Cancel</button>
            <button onClick={() => addSkill(pickableSkills[pickIndex].name, 'catalog', pickableSkills[pickIndex].size)} className="btn-primary" style={{ padding: '8px 18px', fontSize: 13 }}>Add 1 skill</button>
          </div>
        </ModalOverlay>
      )}

      {modal === 'github' && (
        <ModalOverlay onClose={() => setModal(null)}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 15, fontWeight: 700 }}>Add from GitHub URL</span>
            <span onClick={() => setModal(null)} className="link-quiet" style={{ color: 'var(--text-muted)', cursor: 'pointer' }}>✕</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-soft)' }}>Repository or folder URL</label>
            <div className="mono" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 9, padding: '10px 12px', fontSize: 12 }}>github.com/vera-oss/skills/tree/main/pci-review</div>
            <span style={{ fontSize: 12, color: 'var(--success)' }}>✓ Found SKILL.md — pci-review, 18 KB</span>
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button onClick={() => setModal(null)} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13 }}>Cancel</button>
            <button onClick={() => addSkill('pci-review', 'github', '18 KB')} className="btn-primary" style={{ padding: '8px 18px', fontSize: 13 }}>Add skill</button>
          </div>
        </ModalOverlay>
      )}

      {modal === 'upload' && (
        <ModalOverlay onClose={() => setModal(null)}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 15, fontWeight: 700 }}>Upload skill</span>
            <span onClick={() => setModal(null)} className="link-quiet" style={{ color: 'var(--text-muted)', cursor: 'pointer' }}>✕</span>
          </div>
          <div onClick={() => showToast('Upload is stubbed in this prototype')} className="hover-border-violet" style={{ border: '1px dashed var(--border-strong)', borderRadius: 12, padding: '32px 20px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8, background: 'var(--bg-deep)', cursor: 'pointer' }}>
            <span style={{ fontSize: 20, color: 'var(--primary)' }}>↑</span>
            <span style={{ fontSize: 13, color: 'var(--text-soft)', fontWeight: 600 }}>Drop a skill folder or .zip</span>
            <span style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>Must contain SKILL.md · max 2 MB</span>
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button onClick={() => setModal(null)} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13 }}>Cancel</button>
            <button onClick={() => showToast('Upload is stubbed in this prototype')} style={{ background: 'var(--border)', color: 'var(--text-muted)', border: 'none', borderRadius: 999, padding: '8px 18px', fontSize: 13, fontWeight: 600 }}>Upload</button>
          </div>
        </ModalOverlay>
      )}
    </ComposerShell>
  );
}
