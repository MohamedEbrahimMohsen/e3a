import type { HookWarning } from './api';
import { requestJson } from './http';

export interface Engineer {
  id: string;
  slug: string;
  displayName: string;
  description: string | null;
  tags: string[];
  status: string;
  latestVersionId: string | null;
  installCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface EngineerInput {
  slug: string;
  displayName: string;
  description: string | null;
  tags: string[];
}

export interface ImportedItem {
  sourcePath: string;
  targetPath: string;
  category: string;
}

export interface ConvertedItem {
  sourcePath: string;
  targetPath: string;
  reason: string;
}

export interface SkippedItem {
  sourcePath: string;
  reason: string;
}

export interface ImportManifest {
  imported: ImportedItem[];
  converted: ConvertedItem[];
  skipped: SkippedItem[];
  strippedPaths: string[];
  hookWarnings: HookWarning[];
  claudeMdSnippet: string | null;
  uploadedAt: string;
}

export type VersionIncrement = 'Patch' | 'Minor' | 'Major';

export interface PublishStatus {
  versionId: string;
  itemId: string;
  itemType: string;
  versionNumber: number;
  semanticVersion: string;
  status: string;
  zipUrl: string | null;
  zipSha256: string | null;
  sizeBytes: number;
  failureReason: string | null;
  updatedAt: string;
}

export function listMyEngineers(): Promise<Engineer[]> {
  return requestJson<Engineer[]>('/engineers/mine');
}

export function getEngineer(engineerId: string): Promise<Engineer> {
  return requestJson<Engineer>(`/engineers/${encodeURIComponent(engineerId)}`);
}

export function createEngineer(input: EngineerInput): Promise<Engineer> {
  return requestJson<Engineer>('/engineers', { method: 'POST', body: input });
}

export function updateEngineer(engineerId: string, input: EngineerInput): Promise<Engineer> {
  return requestJson<Engineer>(`/engineers/${encodeURIComponent(engineerId)}`, { method: 'PUT', body: input });
}

export function uploadEngineerDraft(engineerId: string, file: File): Promise<ImportManifest> {
  const formData = new FormData();
  formData.append('file', file);
  return requestJson<ImportManifest>(`/engineers/${encodeURIComponent(engineerId)}/upload`, { method: 'POST', formData });
}

export function getImportManifest(engineerId: string): Promise<ImportManifest> {
  return requestJson<ImportManifest>(`/engineers/${encodeURIComponent(engineerId)}/import-manifest`);
}

export function publishEngineer(engineerId: string, increment: VersionIncrement): Promise<PublishStatus> {
  return requestJson<PublishStatus>(`/engineers/${encodeURIComponent(engineerId)}/publish`, { method: 'POST', body: { increment } });
}

export function getPublishStatus(versionId: string): Promise<PublishStatus> {
  return requestJson<PublishStatus>(`/publish/${encodeURIComponent(versionId)}/status`);
}
