const DEFAULT_MAX_UPLOAD_MEGABYTES = 20;

export const config = {
  siteUrl: (import.meta.env.VITE_SITE_URL as string | undefined) ?? 'https://e3a.dev',
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? 'https://localhost:62935/api',
  githubOrgUrl: (import.meta.env.VITE_GITHUB_ORG_URL as string | undefined) ?? 'https://github.com/e3a-registry',
  maxUploadMegabytes: Number(import.meta.env.VITE_MAX_UPLOAD_MEGABYTES ?? DEFAULT_MAX_UPLOAD_MEGABYTES),
} as const;

const siteHost = config.siteUrl.replace(/^https?:\/\//, '');

export type PluginItemType = 'Engineer' | 'Team';

export function marketplaceAddCommand(): string {
  return `/plugin marketplace add ${config.siteUrl}/marketplace.json`;
}

export function installCommand(slug: string, itemType: PluginItemType = 'Engineer'): string {
  return `/plugin install e3a-${itemType === 'Team' ? 'team-' : ''}${slug}@e3a`;
}

export function pinnedMarketplaceCommand(pluginName: string, version: string): string {
  return `/plugin marketplace add ${siteHost}/m/${pluginName}/${version}/marketplace.json`;
}
