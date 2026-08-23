import type { ReactNode } from 'react';

export interface TreeEntry {
  label: ReactNode;
  indent?: boolean;
  muted?: boolean;
}

export function StructureTree({ entries, fontSize = 12.5 }: { entries: TreeEntry[]; fontSize?: number }) {
  return (
    <div className="code-block" style={{ fontSize }}>
      {entries.map((entry, index) => (
        <div key={index} style={{ paddingLeft: entry.indent ? 18 : 0, color: entry.muted ? 'var(--text-muted)' : undefined }}>{entry.label}</div>
      ))}
    </div>
  );
}
