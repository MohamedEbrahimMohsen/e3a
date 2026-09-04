import { createContext, useContext, useState, type ReactNode } from 'react';
import { messageForApiError } from '../lib/errorMessages';
import { DEFAULT_REPORT_REASON, REPORT_REASON_OPTIONS, canSubmitReport, normalizeReportDetails, submitReport, type ReportReason, type ReportTarget } from '../lib/reportsApi';
import { useToast } from './ToastContext';
import { ModalOverlay } from '../components/ModalOverlay';

interface ReportContextValue {
  openReport: (target: ReportTarget) => void;
}

const ReportContext = createContext<ReportContextValue>({ openReport: () => undefined });

export function ReportProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<ReportTarget | null>(null);
  const [reason, setReason] = useState<ReportReason>(DEFAULT_REPORT_REASON);
  const [details, setDetails] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const { showToast } = useToast();

  const close = () => {
    setTarget(null);
    setReason(DEFAULT_REPORT_REASON);
    setDetails('');
    setErrorMessage(null);
  };

  const submit = () => {
    if (!target || submitting) {
      return;
    }
    setSubmitting(true);
    setErrorMessage(null);
    submitReport({ itemType: target.itemType, itemId: target.itemId, reason, details: normalizeReportDetails(details) })
      .then(() => { setSubmitting(false); close(); showToast('Report submitted — thank you'); })
      .catch((error: unknown) => { setSubmitting(false); setErrorMessage(messageForApiError(error)); });
  };

  return (
    <ReportContext.Provider value={{ openReport: setTarget }}>
      {children}
      {target !== null && (
        <ModalOverlay onClose={close}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 15, fontWeight: 700 }}>Report {target.label}</span>
            <button type="button" aria-label="Close" onClick={close} className="link-quiet" style={{ background: 'transparent', border: 'none', padding: 0, fontSize: 13, color: 'var(--text-muted)' }}>✕</button>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-soft)' }}>Reason</label>
            <select value={reason} onChange={event => setReason(event.target.value as ReportReason)} className="hover-border" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 9, padding: '10px 12px', fontSize: 13, color: 'var(--text)', display: 'flex', justifyContent: 'space-between', cursor: 'pointer' }}>
              {REPORT_REASON_OPTIONS.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-soft)' }}>Details</label>
            <textarea value={details} onChange={event => setDetails(event.target.value)} placeholder="Describe what you found, with file paths if possible…" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 9, padding: '10px 12px', fontSize: 13, color: 'var(--text)', minHeight: 72, resize: 'vertical' }} />
          </div>
          {errorMessage !== null && <div style={{ fontSize: 12.5, color: 'var(--danger)' }}>{errorMessage}</div>}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button type="button" onClick={close} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13 }}>Cancel</button>
            <button type="button" onClick={submit} disabled={submitting || !canSubmitReport(reason, details)} className="btn-danger">{submitting ? 'Submitting…' : 'Submit report'}</button>
          </div>
        </ModalOverlay>
      )}
    </ReportContext.Provider>
  );
}

export function useReport(): ReportContextValue {
  return useContext(ReportContext);
}
