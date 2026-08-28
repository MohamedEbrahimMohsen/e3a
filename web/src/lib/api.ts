import { config } from './config';

export interface PageData<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface CatalogEngineer {
  id: string;
  slug: string;
  displayName: string;
  description: string | null;
  tags: string[];
  installCount: number;
  latestVersionId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface HookWarning {
  event: string;
  matcher: string | null;
  command: string | null;
}

export interface CatalogEngineerDetail extends CatalogEngineer {
  ownerUserId: string;
  hookWarnings: HookWarning[];
}

export interface CatalogTag {
  tag: string;
  count: number;
}

export type CatalogSort = 'MostInstalled' | 'Newest';

export interface CatalogQuery {
  searchText?: string;
  tags?: string[];
  sort?: CatalogSort;
  pageNumber?: number;
  pageSize?: number;
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${config.apiBaseUrl}${path}`);
  if (!response.ok) {
    throw new ApiError(response.status);
  }
  return (await response.json()) as T;
}

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number) {
    super(`API request failed with status ${status}`);
    this.status = status;
  }
}

export function getCatalog(query: CatalogQuery): Promise<PageData<CatalogEngineer>> {
  const parameters = new URLSearchParams();
  if (query.searchText) {
    parameters.set('q', query.searchText);
  }
  for (const tag of query.tags ?? []) {
    parameters.append('tag', tag);
  }
  if (query.sort) {
    parameters.set('sort', query.sort);
  }
  if (query.pageNumber) {
    parameters.set('page', String(query.pageNumber));
  }
  if (query.pageSize) {
    parameters.set('pageSize', String(query.pageSize));
  }
  const queryString = parameters.toString();
  return getJson(`/catalog${queryString ? `?${queryString}` : ''}`);
}

export function getCatalogEngineer(slug: string): Promise<CatalogEngineerDetail> {
  return getJson(`/catalog/${encodeURIComponent(slug)}`);
}

export function getCatalogTags(): Promise<CatalogTag[]> {
  return getJson('/catalog/tags');
}

const cardEmojis = ['🧱', '⚛️', '🛡️', '🧪', '📐', '🚀', '🗃️', '✍️', '🔍', '💳', '🧭', '🤖'];

export function emojiFor(slug: string): string {
  let hash = 0;
  for (const character of slug) {
    hash = (hash * 31 + character.charCodeAt(0)) >>> 0;
  }
  return cardEmojis[hash % cardEmojis.length];
}
