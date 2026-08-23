import type { ReactNode } from 'react';

export function ModalOverlay({ onClose, children, width = 400 }: { onClose: () => void; children: ReactNode; width?: number }) {
  return (
    <div onClick={onClose} style={{ position: 'fixed', inset: 0, background: 'rgba(6,6,9,0.72)', backdropFilter: 'blur(3px)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50, animation: 'fadeIn 0.15s ease' }}>
      <div onClick={event => event.stopPropagation()} style={{ width, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: 16, padding: 24, display: 'flex', flexDirection: 'column', gap: 16, boxShadow: '0 24px 60px rgba(0,0,0,0.5)' }}>
        {children}
      </div>
    </div>
  );
}
