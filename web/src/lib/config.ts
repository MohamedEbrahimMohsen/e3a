export const config = {
  siteUrl: (import.meta.env.VITE_SITE_URL as string | undefined) ?? 'https://e3a.dev',
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? 'https://localhost:62935/api',
  githubOrgUrl: (import.meta.env.VITE_GITHUB_ORG_URL as string | undefined) ?? 'https://github.com/e3a-registry',
} as const;

const siteHost = config.siteUrl.replace(/^https?:\/\//, '');

export function marketplaceAddCommand(): string {
  return `/plugin marketplace add ${config.siteUrl}/marketplace.json`;
}

export function installCommand(slug: string): string {
  return `/plugin install e3a-${slug}@e3a`;
}

export function pinnedMarketplaceCommand(pluginName: string, version: string): string {
  return `/plugin marketplace add ${siteHost}/m/${pluginName}/${version}/marketplace.json`;
}
