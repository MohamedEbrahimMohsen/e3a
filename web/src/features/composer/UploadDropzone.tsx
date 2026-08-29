import { useRef, useState } from 'react';

const pickerStyle: React.CSSProperties = { background: 'none', border: 'none', padding: 0, fontFamily: 'inherit', fontSize: 13, fontWeight: 600, color: 'var(--text-soft)' };

export function UploadDropzone({ onFile, disabled, busy, maxMegabytes }: { onFile: (file: File) => void; disabled: boolean; busy: boolean; maxMegabytes: number }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragActive, setDragActive] = useState(false);

  const accept = (file: File | undefined) => {
    if (file) {
      onFile(file);
    }
  };

  const handleDrop = (event: React.DragEvent) => {
    event.preventDefault();
    setDragActive(false);
    if (!disabled && !busy) {
      accept(event.dataTransfer.files[0]);
    }
  };

  const handleDragOver = (event: React.DragEvent) => {
    event.preventDefault();
    if (!disabled && !busy) {
      setDragActive(true);
    }
  };

  const openPicker = (event: React.MouseEvent) => {
    event.stopPropagation();
    inputRef.current?.click();
  };

  const borderColor = dragActive ? 'rgba(139,92,246,0.55)' : 'var(--border-strong)';

  return (
    <div
      onClick={() => { if (!disabled && !busy) { inputRef.current?.click(); } }}
      onDragOver={handleDragOver}
      onDragLeave={() => setDragActive(false)}
      onDrop={handleDrop}
      className={disabled ? undefined : 'hover-border-violet'}
      style={{ border: `1px dashed ${disabled ? 'var(--border)' : borderColor}`, borderRadius: 12, padding: '44px 20px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8, background: 'var(--bg-deep)', cursor: disabled || busy ? 'default' : 'pointer', opacity: disabled ? 0.55 : 1 }}
    >
      <input ref={inputRef} type="file" accept=".zip" style={{ display: 'none' }} onChange={event => { accept(event.target.files?.[0]); event.target.value = ''; }} />
      {busy
        ? <><span className="spinner" /><span style={{ fontSize: 13, color: 'var(--text-soft)', fontWeight: 600 }}>Uploading…</span></>
        : (
          <>
            <span style={{ fontSize: 20, color: disabled ? 'var(--text-muted)' : 'var(--primary)' }}>↑</span>
            <button type="button" onClick={openPicker} disabled={disabled || busy} style={{ ...pickerStyle, cursor: disabled || busy ? 'default' : 'pointer' }}>Drop your zipped .claude folder</button>
            <span style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>{disabled ? 'Save the draft first' : `.zip · max ${maxMegabytes} MB`}</span>
          </>
        )}
    </div>
  );
}
