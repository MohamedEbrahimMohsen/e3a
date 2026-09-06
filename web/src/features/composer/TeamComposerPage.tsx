import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useToast } from '../../app/ToastContext';
import { StructureTree } from '../../components/StructureTree';
import { emojiFor, getCatalog, type CatalogEngineer } from '../../lib/api';
import { config } from '../../lib/config';
import { messageForApiError } from '../../lib/errorMessages';
import { toSlug } from '../../lib/slug';
import { createTeam, getTeam, publishTeam, setTeamMembers, updateTeam, type TeamDetail, type TeamInput, type VersionIncrement } from '../../lib/workspaceApi';
import { ComposerShell } from './ComposerShell';
import { addMember, isMember, memberVersionLabel, moveMemberDraft, removeMemberDraft, teamStructurePaths, toMemberDrafts, toMemberSelections, type TeamMemberDraft } from './teamMembers';

const MEMBER_SEARCH_DEBOUNCE_MS = 250;
const MEMBER_SEARCH_PAGE_SIZE = 6;
const increments: VersionIncrement[] = ['Patch', 'Minor', 'Major'];
const labelStyle: React.CSSProperties = { fontSize: 13, fontWeight: 600, color: 'var(--text-soft)' };

export function TeamComposerPage() {
  const navigate = useNavigate();
  const { showToast } = useToast();
  const routeTeamId = useParams().teamId ?? null;

  const [teamId, setTeamId] = useState<string | null>(routeTeamId);
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [tagDraft, setTagDraft] = useState('');
  const [serverSlug, setServerSlug] = useState<string | null>(null);
  const [members, setMembers] = useState<TeamMemberDraft[]>([]);
  const [memberQuery, setMemberQuery] = useState('');
  const [candidates, setCandidates] = useState<CatalogEngineer[]>([]);
  const [increment, setIncrement] = useState<VersionIncrement>('Patch');
  const [saving, setSaving] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [lastSaved, setLastSaved] = useState('never');
  const [loadStatus, setLoadStatus] = useState<'loading' | 'ready' | 'failed'>(routeTeamId ? 'loading' : 'ready');

  const slug = serverSlug ?? toSlug(displayName);
  const selfSavedTeamId = useRef<string | null>(null);

  useEffect(() => {
    if (!routeTeamId || selfSavedTeamId.current === routeTeamId) {
      return;
    }
    let cancelled = false;
    getTeam(routeTeamId)
      .then(team => {
        if (cancelled) {
          return;
        }
        setTeamId(team.id);
        setDisplayName(team.displayName);
        setDescription(team.description ?? '');
        setTags(team.tags);
        setServerSlug(team.slug);
        setMembers(toMemberDrafts(team.members));
        setLoadStatus('ready');
      })
      .catch(error => { if (!cancelled) { setLoadStatus('failed'); setErrorMessage(messageForApiError(error)); } });
    return () => { cancelled = true; };
  }, [routeTeamId]);

  useEffect(() => {
    let cancelled = false;
    const timer = window.setTimeout(() => {
      getCatalog({ searchText: memberQuery.trim() || undefined, pageNumber: 1, pageSize: MEMBER_SEARCH_PAGE_SIZE })
        .then(result => { if (!cancelled) { setCandidates(result.items); } })
        .catch(() => { if (!cancelled) { setCandidates([]); } });
    }, memberQuery ? MEMBER_SEARCH_DEBOUNCE_MS : 0);
    return () => { cancelled = true; window.clearTimeout(timer); };
  }, [memberQuery]);

  const persist = async (): Promise<TeamDetail> => {
    const isNewTeam = teamId === null;
    const input: TeamInput = { slug, displayName, description: description || null, tags };
    const saved = await (teamId ? updateTeam(teamId, input) : createTeam(input));
    setServerSlug(saved.slug);
    if (isNewTeam) {
      setTeamId(saved.id);
    }
    const detail = await setTeamMembers(saved.id, toMemberSelections(members));
    setMembers(toMemberDrafts(detail.members));
    setLastSaved('just now');
    if (isNewTeam) {
      selfSavedTeamId.current = saved.id;
      navigate(`/workspace/teams/${saved.id}`, { replace: true });
    }
    return detail;
  };

  const handleSaveDraft = () => {
    if (saving || publishing) {
      return;
    }
    setSaving(true);
    setErrorMessage(null);
    persist()
      .then(() => showToast('Draft saved'))
      .catch(error => setErrorMessage(messageForApiError(error)))
      .finally(() => setSaving(false));
  };

  const handlePublish = () => {
    if (saving || publishing) {
      return;
    }
    setPublishing(true);
    setErrorMessage(null);
    persist()
      .then(detail => publishTeam(detail.id, increment))
      .then(result => navigate(`/workspace/publish?versionId=${result.versionId}`))
      .catch(error => { setErrorMessage(messageForApiError(error)); setPublishing(false); });
  };

  const handleAddMember = (engineer: CatalogEngineer) => {
    const outcome = addMember(members, engineer, config.maxTeamMembers);
    if (outcome.problem !== null) {
      setErrorMessage(outcome.problem);
      return;
    }
    setErrorMessage(null);
    setMembers(outcome.drafts);
    showToast(`Added ${engineer.slug}`);
  };

  const addTag = () => {
    const tag = toSlug(tagDraft);
    if (tag && !tags.includes(tag)) {
      setTags([...tags, tag]);
    }
    setTagDraft('');
  };

  if (loadStatus === 'loading') {
    return <div className="page" style={{ alignItems: 'center', color: 'var(--text-muted)', fontSize: 13.5 }}>Loading…</div>;
  }

  return (
    <ComposerShell
      title={teamId ? displayName || 'Team' : 'New team'}
      lastSaved={lastSaved}
      onSaveDraft={handleSaveDraft}
      onPublish={handlePublish}
      saveDisabled={saving || publishing}
      publishDisabled={publishing || saving || members.length === 0 || displayName.trim().length === 0}
      publishLabel={publishing ? 'Publishing…' : 'Publish'}
      statusLabel={saving ? 'Saving…' : 'Draft'}
    >
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr', flex: 1 }}>
        <div style={{ padding: '36px 40px', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: 22 }}>
          {errorMessage && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, background: 'rgba(248,113,113,0.06)', border: '1px solid rgba(248,113,113,0.3)', borderRadius: 10, padding: '11px 14px', fontSize: 12.5, color: 'var(--text-soft)' }}>
              <span style={{ flex: 1 }}>{errorMessage}</span>
              <button type="button" onClick={() => setErrorMessage(null)} aria-label="Dismiss error" className="link-quiet" style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', fontSize: 13 }}>×</button>
            </div>
          )}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle} htmlFor="team-name">Team name</label>
            <input id="team-name" value={displayName} onChange={event => setDisplayName(event.target.value)} placeholder="Fintech Launch Crew" className="input-field" />
            <span className="mono" style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>slug: {slug || '—'}</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle} htmlFor="team-description">Description</label>
            <textarea id="team-description" value={description} onChange={event => setDescription(event.target.value)} className="input-field" style={{ minHeight: 72, lineHeight: 1.5, resize: 'vertical' }} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle} htmlFor="team-tag">Tags</label>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '9px 12px', display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
              {tags.map(tag => (
                <button key={tag} type="button" onClick={() => setTags(tags.filter(other => other !== tag))} className="tag-chip" style={{ fontSize: 12, color: 'var(--text)', background: 'var(--border)', border: 'none', padding: '3px 10px', cursor: 'pointer' }}>{tag} ×</button>
              ))}
              <input
                id="team-tag"
                value={tagDraft}
                onChange={event => setTagDraft(event.target.value)}
                onKeyDown={event => { if (event.key === 'Enter' || event.key === ',') { event.preventDefault(); addTag(); } }}
                onBlur={addTag}
                placeholder="Add tag…"
                style={{ flex: 1, minWidth: 90, background: 'transparent', border: 'none', color: 'var(--text)', fontSize: 13, padding: 0 }}
              />
            </div>
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'flex-start', background: 'rgba(34,211,238,0.05)', border: '1px solid rgba(34,211,238,0.18)', borderRadius: 12, padding: '14px 16px', fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
            <span style={{ color: 'var(--accent)' }}>ⓘ</span>
            <span>Publishing snapshots each member at its pinned version. To ship newer members later, publish a new team version.</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle} htmlFor="version-increment">Version increment</label>
            <select id="version-increment" value={increment} onChange={event => setIncrement(event.target.value as VersionIncrement)} className="input-field" style={{ maxWidth: 180 }}>
              {increments.map(option => <option key={option} value={option}>{option}</option>)}
            </select>
          </div>
        </div>
        <div style={{ padding: '36px 40px', display: 'flex', flexDirection: 'column', gap: 22, background: 'var(--bg-panel)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label style={labelStyle} htmlFor="member-search">Add members</label>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '4px 14px' }}>
              <span style={{ color: 'var(--text-muted)' }}>⌕</span>
              <input id="member-search" value={memberQuery} onChange={event => setMemberQuery(event.target.value)} placeholder="Search published engineers…" style={{ flex: 1, background: 'transparent', border: 'none', color: 'var(--text)', fontSize: 13.5, padding: '8px 0' }} />
            </div>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
              {candidates.length === 0 && <div style={{ padding: '11px 14px', fontSize: 12.5, color: 'var(--text-muted)' }}>No published engineers match that search.</div>}
              {candidates.map(candidate => (
                <div key={candidate.id} className="hover-row" style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '11px 14px', borderBottom: '1px solid var(--surface-elevated)' }}>
                  <span style={{ fontSize: 16 }}>{emojiFor(candidate.slug)}</span>
                  <span className="mono" style={{ fontSize: 12.5 }}>{candidate.slug}</span>
                  <span style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>{candidate.displayName}</span>
                  <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
                    {isMember(members, candidate.id)
                      ? <span style={{ fontSize: 12, color: 'var(--success)', fontWeight: 600, padding: '4px 8px' }}>Added ✓</span>
                      : <button type="button" onClick={() => handleAddMember(candidate)} className="btn-primary" style={{ borderRadius: 6, padding: '4px 12px', fontSize: 12 }}>Add</button>}
                  </div>
                </div>
              ))}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label style={labelStyle}>Members <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>{members.length} / {config.maxTeamMembers}</span></label>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {members.length === 0 && <span style={{ fontSize: 12.5, color: 'var(--text-muted)' }}>Add at least one published engineer before publishing.</span>}
              {members.map((draft, index) => (
                <div key={draft.engineerId} className="hover-border fade-in" style={{ display: 'flex', alignItems: 'center', gap: 12, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '12px 14px' }}>
                  <span className="mono" style={{ fontSize: 11, color: 'var(--text-muted)' }}>{String(index + 1).padStart(2, '0')}</span>
                  <span style={{ fontSize: 16 }}>{emojiFor(draft.engineerSlug)}</span>
                  <span className="mono" style={{ fontSize: 12.5 }}>{draft.engineerSlug}</span>
                  <span className="version-badge" style={{ marginLeft: 'auto' }}>{memberVersionLabel(draft)}</span>
                  <button type="button" onClick={() => setMembers(moveMemberDraft(members, index, index - 1))} disabled={index === 0} aria-label={`Move ${draft.engineerSlug} up`} className="btn-secondary" style={{ padding: '2px 9px', fontSize: 12 }}>↑</button>
                  <button type="button" onClick={() => setMembers(moveMemberDraft(members, index, index + 1))} disabled={index === members.length - 1} aria-label={`Move ${draft.engineerSlug} down`} className="btn-secondary" style={{ padding: '2px 9px', fontSize: 12 }}>↓</button>
                  <button type="button" onClick={() => setMembers(removeMemberDraft(members, draft.engineerId))} aria-label={`Remove ${draft.engineerSlug}`} className="link-danger-hover" style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', fontSize: 13 }}>×</button>
                </div>
              ))}
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <label style={labelStyle}>Structure preview</label>
            <StructureTree fontSize={12} entries={teamStructurePaths(members).map(path => ({ label: path, indent: path.startsWith('skills/') }))} />
          </div>
        </div>
      </div>
    </ComposerShell>
  );
}
