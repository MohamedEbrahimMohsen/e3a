import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useToast } from '../../app/ToastContext';
import { StructureTree } from '../../components/StructureTree';
import { config } from '../../lib/config';
import { messageForApiError } from '../../lib/errorMessages';
import { ApiError } from '../../lib/http';
import { toSlug } from '../../lib/slug';
import { createEngineer, getEngineer, getImportManifest, publishEngineer, updateEngineer, uploadEngineerDraft, type ImportManifest, type VersionIncrement } from '../../lib/workspaceApi';
import { ComposerShell } from './ComposerShell';
import { ImportManifestPanel } from './ImportManifestPanel';
import { toStructurePaths } from './importManifestStructure';
import { UploadDropzone } from './UploadDropzone';
import { validateUploadFile } from './uploadFileValidation';

const increments: VersionIncrement[] = ['Patch', 'Minor', 'Major'];
const labelStyle: React.CSSProperties = { fontSize: 13, fontWeight: 600, color: 'var(--text-soft)' };

export function EngineerComposerPage() {
  const navigate = useNavigate();
  const { showToast } = useToast();
  const routeEngineerId = useParams().engineerId ?? null;

  const [engineerId, setEngineerId] = useState<string | null>(routeEngineerId);
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [tagDraft, setTagDraft] = useState('');
  const [serverSlug, setServerSlug] = useState<string | null>(null);
  const [manifest, setManifest] = useState<ImportManifest | null>(null);
  const [increment, setIncrement] = useState<VersionIncrement>('Patch');
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [lastSaved, setLastSaved] = useState('never');
  const [loadStatus, setLoadStatus] = useState<'loading' | 'ready' | 'failed'>(routeEngineerId ? 'loading' : 'ready');

  const slug = serverSlug ?? toSlug(displayName);

  useEffect(() => {
    if (!routeEngineerId) {
      return;
    }
    let cancelled = false;
    getEngineer(routeEngineerId)
      .then(engineer => {
        if (cancelled) {
          return;
        }
        setEngineerId(engineer.id);
        setDisplayName(engineer.displayName);
        setDescription(engineer.description ?? '');
        setTags(engineer.tags);
        setServerSlug(engineer.slug);
        setLoadStatus('ready');
        return getImportManifest(engineer.id)
          .then(result => { if (!cancelled) { setManifest(result); } })
          .catch(error => {
            if (cancelled) {
              return;
            }
            if (error instanceof ApiError && error.code === 'ENGINEER_DRAFT_NOT_UPLOADED') {
              setManifest(null);
              return;
            }
            setErrorMessage(messageForApiError(error));
          });
      })
      .catch(error => { if (!cancelled) { setLoadStatus('failed'); setErrorMessage(messageForApiError(error)); } });
    return () => { cancelled = true; };
  }, [routeEngineerId]);

  const handleSaveDraft = () => {
    const input = { slug, displayName, description: description || null, tags };
    setSaving(true);
    const saved = engineerId ? updateEngineer(engineerId, input) : createEngineer(input);
    saved
      .then(result => {
        setServerSlug(result.slug);
        setLastSaved('just now');
        showToast('Draft saved');
        if (!engineerId) {
          setEngineerId(result.id);
          navigate(`/workspace/engineers/${result.id}`, { replace: true });
        }
      })
      .catch(error => setErrorMessage(messageForApiError(error)))
      .finally(() => setSaving(false));
  };

  const handleFile = (file: File) => {
    if (!engineerId) {
      return;
    }
    const problem = validateUploadFile(file, config.maxUploadMegabytes);
    if (problem) {
      setErrorMessage(problem);
      return;
    }
    setUploading(true);
    uploadEngineerDraft(engineerId, file)
      .then(result => { setManifest(result); showToast('Upload imported'); })
      .catch(error => setErrorMessage(messageForApiError(error)))
      .finally(() => setUploading(false));
  };

  const handlePublish = () => {
    if (!engineerId) {
      return;
    }
    setPublishing(true);
    publishEngineer(engineerId, increment)
      .then(result => navigate(`/workspace/publish?versionId=${result.versionId}`))
      .catch(error => { setErrorMessage(messageForApiError(error)); setPublishing(false); });
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
      title={engineerId ? displayName || 'Engineer' : 'New engineer'}
      lastSaved={lastSaved}
      onSaveDraft={handleSaveDraft}
      onPublish={handlePublish}
      publishDisabled={!engineerId || manifest === null || publishing}
      publishLabel={publishing ? 'Publishing…' : 'Publish'}
      statusLabel={saving ? 'Saving…' : 'Draft'}
    >
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', flex: 1 }}>
        <div style={{ padding: '36px 40px', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: 22 }}>
          {errorMessage && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, background: 'rgba(248,113,113,0.06)', border: '1px solid rgba(248,113,113,0.3)', borderRadius: 10, padding: '11px 14px', fontSize: 12.5, color: 'var(--text-soft)' }}>
              <span style={{ flex: 1 }}>{errorMessage}</span>
              <span onClick={() => setErrorMessage(null)} className="link-quiet" style={{ color: 'var(--text-muted)' }}>×</span>
            </div>
          )}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Name</label>
            <input value={displayName} onChange={event => setDisplayName(event.target.value)} placeholder="Payments Engineer" className="input-field" />
            <span className="mono" style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>slug: {slug || '—'}</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Description</label>
            <textarea value={description} onChange={event => setDescription(event.target.value)} className="input-field" style={{ minHeight: 72, lineHeight: 1.5, resize: 'vertical' }} />
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle}>Tags</label>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 10, padding: '9px 12px', display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
              {tags.map(tag => (
                <span key={tag} onClick={() => setTags(tags.filter(other => other !== tag))} className="tag-chip" style={{ fontSize: 12, color: 'var(--text)', background: 'var(--border)', padding: '3px 10px', cursor: 'pointer' }}>{tag} ×</span>
              ))}
              <input
                value={tagDraft}
                onChange={event => setTagDraft(event.target.value)}
                onKeyDown={event => { if (event.key === 'Enter' || event.key === ',') { event.preventDefault(); addTag(); } }}
                onBlur={addTag}
                placeholder="Add tag…"
                style={{ flex: 1, minWidth: 90, background: 'transparent', border: 'none', color: 'var(--text)', fontSize: 13, padding: 0 }}
              />
            </div>
          </div>
        </div>
        <div style={{ padding: '36px 40px', display: 'flex', flexDirection: 'column', gap: 22, background: 'var(--bg-panel)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={labelStyle} htmlFor="version-increment">Version increment</label>
            <select id="version-increment" value={increment} onChange={event => setIncrement(event.target.value as VersionIncrement)} className="input-field" style={{ maxWidth: 180 }}>
              {increments.map(option => <option key={option} value={option}>{option}</option>)}
            </select>
          </div>
          {manifest === null
            ? <UploadDropzone onFile={handleFile} disabled={engineerId === null} busy={uploading} maxMegabytes={config.maxUploadMegabytes} />
            : (
              <>
                <ImportManifestPanel manifest={manifest} onReplace={() => setManifest(null)} />
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <label style={labelStyle}>Structure preview</label>
                  <StructureTree fontSize={12} entries={toStructurePaths(manifest).map(path => ({ label: path, indent: path.includes('/') }))} />
                </div>
              </>
            )}
        </div>
      </div>
    </ComposerShell>
  );
}
