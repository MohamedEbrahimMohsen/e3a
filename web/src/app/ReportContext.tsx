import { createContext, useContext, useState, type ReactNode } from 'react';
import { reportReasons } from '../lib/catalog';
import { useToast } from './ToastContext';
import { ModalOverlay } from '../components/ModalOverlay';

interface ReportContextValue {
  openReport: (target: string) => void;
}

const ReportContext = createContext<ReportContextValue>({ openReport: () => undefined });

export function ReportProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<string | null>(null);
  const [reasonIndex, setReasonIndex] = useState(0);
  const { showToast } = useToast();

  const close = () => setTarget(null);
  const submit = () => {
    close();
    showToast('Report submitted — thank you');
  };

  return (
    <ReportContext.Provider value={{ openReport: setTarget }}>
      {children}
      {target !== null && (
        <ModalOverlay onClose={close}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 15, fontWeight: 700 }}>Report {target}</span>
            <span onClick={close} className="link-quiet" style={{ color: 'var(--text-muted)' }}>✕</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-soft)' }}>Reason</label>
            <div onClick={() => setReasonIndex((reasonIndex + 1) % reportReasons.length)} className="hover-border" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 9, padding: '10px 12px', fontSize: 13, color: 'var(--text)', display: 'flex', justifyContent: 'space-between', cursor: 'pointer' }}>
            {reportReasons[reasonIndex]} <span style={{ color: 'var(--text-muted)' }}>▾</span>
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <label style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-soft)' }}>Details</label>
            <textarea placeholder="Describe what you found, with file paths if possible…" style={{ background: 'var(--bg-deep)', border: '1px solid var(--border)', borderRadius: 9, padding: '10px 12px', fontSize: 13, color: 'var(--text)', minHeight: 72, resize: 'vertical' }} />
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button onClick={close} className="btn-secondary" style={{ padding: '8px 18px', fontSize: 13 }}>Cancel</button>
            <button onClick={submit} className="btn-danger">Submit report</button>
          </div>
        </ModalOverlay>
      )}
    </ReportContext.Provider>
  );
}

export function useReport(): ReportContextValue {
  return useContext(ReportContext);
}
