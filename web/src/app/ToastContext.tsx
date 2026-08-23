import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';

interface ToastContextValue {
  showToast: (message: string) => void;
}

const ToastContext = createContext<ToastContextValue>({ showToast: () => undefined });

const TOAST_DURATION_MS = 1800;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState<string | null>(null);
  const timerRef = useRef<number | undefined>(undefined);

  const showToast = useCallback((next: string) => {
    setMessage(next);
    window.clearTimeout(timerRef.current);
    timerRef.current = window.setTimeout(() => setMessage(null), TOAST_DURATION_MS);
  }, []);

  return (
    <ToastContext.Provider value={{ showToast }}>
      {children}
      {message && (
        <div style={{ position: 'fixed', bottom: 28, left: '50%', transform: 'translate(-50%, 0)', background: 'var(--surface-elevated)', border: '1px solid var(--border-strong)', borderRadius: 999, padding: '10px 22px', fontSize: 13, color: 'var(--text)', zIndex: 60, animation: 'toastIn 0.2s ease', boxShadow: '0 12px 32px rgba(0,0,0,0.5)' }}>
          {message}
        </div>
      )}
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  return useContext(ToastContext);
}
