import type { ImportManifest } from '../../lib/workspaceApi';

export function toStructurePaths(manifest: ImportManifest): string[] {
  const targetPaths = [...manifest.imported, ...manifest.converted].map(item => item.targetPath);
  return [...new Set(targetPaths)].sort((left, right) => left.localeCompare(right));
}
