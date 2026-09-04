import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { InstallBlock } from '../../components/InstallBlock';
import { installCommand } from '../../lib/config';
import { messageForApiError } from '../../lib/errorMessages';
import { getEngineer, getPublishStatus, type Engineer, type PublishStatus } from '../../lib/workspaceApi';
import { failureText, isFailedStatus, isTerminalStatus, PUBLISH_STEP_LABELS, stepIndexFor } from './publishStage';

const POLL_INTERVAL_MS = 2000;
const POLL_MAX_ATTEMPTS = 60;
const POLL_TIMEOUT_MESSAGE = 'This publish is taking longer than expected. Refresh to check again.';

export function PublishStatusPage() {
  const versionId = useSearchParams()[0].get('versionId');
  const [status, setStatus] = useState<PublishStatus | null>(null);
  const [engineer, setEngineer] = useState<Engineer | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!versionId) {
      return;
    }
    let cancelled = false;
    let timer: number | undefined;
    let attempts = 0;

    const tick = async () => {
      try {
        const result = await getPublishStatus(versionId);
        if (cancelled) {
          return;
        }
        setStatus(result);
        if (isTerminalStatus(result.status)) {
          if (result.status === 'Published') {
            getEngineer(result.itemId).then(loaded => { if (!cancelled) { setEngineer(loaded); } }).catch(() => undefined);
          }
          return;
        }
        attempts += 1;
        if (attempts >= POLL_MAX_ATTEMPTS) {
          setErrorMessage(POLL_TIMEOUT_MESSAGE);
          return;
        }
        timer = window.setTimeout(() => void tick(), POLL_INTERVAL_MS);
      } catch (error) {
        if (!cancelled) {
          setErrorMessage(messageForApiError(error));
        }
      }
    };

    void tick();
    return () => { cancelled = true; window.clearTimeout(timer); };
  }, [versionId]);

  if (!versionId) {
    return (
      <div className="page fade-in" style={{ alignItems: 'center', gap: 12, textAlign: 'center' }}>
        <span style={{ fontSize: 16, fontWeight: 700 }}>No publish selected</span>
        <Link to="/workspace" className="link-quiet" style={{ fontSize: 13, color: 'var(--text-secondary)' }}>← Back to workspace</Link>
      </div>
    );
  }

  const stage = status ? stepIndexFor(status.status) : -1;
  const failed = status !== null && isFailedStatus(status.status);

  return (
    <div className="fade-in" style={{ maxWidth: 840, width: '100%', margin: '0 auto', padding: '56px 48px 72px', display: 'flex', flexDirection: 'column', gap: 24 }}>
      <Link to="/workspace" className="link-quiet" style={{ fontSize: 13, color: 'var(--text-secondary)' }}>← Back to workspace</Link>
      {errorMessage && (
        <div style={{ background: 'rgba(248,113,113,0.06)', border: '1px solid rgba(248,113,113,0.3)', borderRadius: 10, padding: '11px 14px', fontSize: 12.5, color: 'var(--text-soft)' }}>{errorMessage}</div>
      )}
      <div className="card" style={{ borderRadius: 16, padding: '28px 32px', display: 'flex', flexDirection: 'column', gap: 22 }}>
        <div style={{ display: 'flex', alignItems: 'center' }}>
          {PUBLISH_STEP_LABELS.map((label, index) => {
            const done = stage > index || (stage >= 2 && index === 2);
            const active = stage === index && stage < 2;
            return (
              <div key={label} style={{ display: 'flex', alignItems: 'center', flex: index < 2 ? 1 : 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  {done && <span style={{ width: 28, height: 28, borderRadius: '50%', background: 'rgba(52,211,153,0.12)', border: '1px solid rgba(52,211,153,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--success)', fontSize: 13 }}>✓</span>}
                  {active && <span className="spinner" />}
                  {!done && !active && <span style={{ width: 28, height: 28, borderRadius: '50%', border: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-muted)', fontSize: 12 }}>{index + 1}</span>}
                  <span style={{ fontSize: 13.5, fontWeight: 600, color: stage >= index ? 'var(--text)' : 'var(--text-muted)', transition: 'color 0.3s ease' }}>{label}</span>
                </div>
                {index < 2 && <div style={{ flex: 1, height: 1, background: 'var(--border)', margin: '0 16px' }} />}
              </div>
            );
          })}
        </div>
        <div className="mono" style={{ fontSize: 12, color: 'var(--text-muted)' }}>{status ? `${status.status} · v${status.semanticVersion}` : 'Checking status…'}</div>
      </div>
      {status !== null && status.status === 'Published' && (
        <div style={{ animation: 'fadeIn 0.25s ease', background: 'rgba(52,211,153,0.04)', border: '1px solid rgba(52,211,153,0.25)', borderRadius: 16, padding: '28px 32px', display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <span style={{ width: 36, height: 36, borderRadius: '50%', background: 'rgba(52,211,153,0.12)', border: '1px solid rgba(52,211,153,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--success)', fontSize: 17 }}>✓</span>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <span style={{ fontSize: 16, fontWeight: 700 }}>Published</span>
              <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>{engineer ? `${engineer.slug} is live in the catalog` : 'Your engineer is live in the catalog'}</span>
            </div>
            <span className="version-badge" style={{ marginLeft: 'auto', fontSize: 12, padding: '3px 9px' }}>v{status.semanticVersion}</span>
          </div>
          {engineer !== null && (
            <>
              <InstallBlock single line2={installCommand(engineer.slug, 'Engineer')} />
              <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
                <Link to="/catalog"><button className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13 }}>View in catalog</button></Link>
              </div>
            </>
          )}
        </div>
      )}
      {failed && status !== null && (
        <div style={{ animation: 'fadeIn 0.25s ease', background: 'rgba(248,113,113,0.04)', border: '1px solid rgba(248,113,113,0.3)', borderRadius: 16, padding: '28px 32px', display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
            <span style={{ width: 36, height: 36, borderRadius: '50%', background: 'rgba(248,113,113,0.12)', border: '1px solid rgba(248,113,113,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--danger)', fontSize: 16 }}>✕</span>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <span style={{ fontSize: 16, fontWeight: 700 }}>Publish {status.status.toLowerCase()}</span>
              <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>{failureText(status.failureReason)}</span>
            </div>
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Link to={`/workspace/engineers/${status.itemId}`}><button className="btn-primary" style={{ padding: '9px 22px' }}>Fix and republish</button></Link>
          </div>
        </div>
      )}
    </div>
  );
}
