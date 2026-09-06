import type { CatalogItem, FaqEntry, TeamMemberInfo, VersionInfo } from './types';

function engineer(emoji: string, name: string, author: string, description: string, tags: string[], version: string, installs: number): CatalogItem {
  return { emoji, name, author, description, tags, version, installs };
}

export const engineers: CatalogItem[] = [
  engineer('🧱', 'backend-engineer', 'mohamed-dive', 'Senior .NET backend engineer — CQRS vertical slices, EF Core, clean error contracts.', ['dotnet', 'cqrs', 'api'], 'v3.0.0', 1204),
  engineer('⚛️', 'react-frontend', 'mohamed-dive', 'React 18 + TS strict. TanStack Query, RHF + Zod, shadcn/ui, AR/EN i18n.', ['react', 'typescript'], 'v2.4.1', 987),
  engineer('🛡️', 'security-reviewer', 'vera-oss', 'Threat-models every diff. OWASP, secrets scanning, dependency audits.', ['security', 'review'], 'v1.2.0', 2310),
  engineer('🧪', 'test-author', 'kenji-dev', 'Writes the tests you skipped. Unit, integration, property-based.', ['testing', 'tdd'], 'v4.0.2', 640),
  engineer('📐', 'api-designer', 'sofia-b', 'REST and gRPC contracts, versioning strategy, OpenAPI-first.', ['api', 'openapi'], 'v1.0.3', 38),
  engineer('🚀', 'devops-runner', 'ops-guild', 'CI/CD pipelines, Docker, Terraform modules, rollback playbooks.', ['devops', 'ci'], 'v2.0.0', 412),
  engineer('🗃️', 'data-migrator', 'sofia-b', 'Zero-downtime schema migrations and backfills, with rollback plans.', ['sql', 'migrations'], 'v0.9.1', 21),
  engineer('✍️', 'docs-writer', 'vera-oss', 'ADRs, READMEs, runbooks. Keeps docs in sync with the code.', ['docs'], 'v1.1.0', 77),
  engineer('🔍', 'code-archaeologist', 'kenji-dev', 'Explains legacy code and maps hidden coupling before you refactor.', ['legacy', 'analysis'], 'v1.3.2', 530),
];

export const teams: CatalogItem[] = [
  { emoji: '🏗️', name: 'full-stack-squad', author: 'mohamed-dive', description: 'Backend + frontend + tests. Ships a vertical slice end to end.', tags: ['fullstack'], version: 'v1.2.0', installs: 864, team: true, members: ['🧱', '⚛️', '🧪'] },
  { emoji: '🚨', name: 'incident-response', author: 'ops-guild', description: 'Triage, hotfix, postmortem. Wired for on-call from the first alert.', tags: ['sre', 'oncall'], version: 'v2.0.1', installs: 301, team: true, members: ['🚀', '🔍', '✍️'] },
  { emoji: '🧭', name: 'legacy-rescue', author: 'kenji-dev', description: 'Understand, test, then refactor a legacy codebase safely.', tags: ['legacy', 'refactor'], version: 'v1.0.0', installs: 145, team: true, members: ['🔍', '🧪', '🧱'] },
];

export const allItems: CatalogItem[] = [engineers[2], teams[0], engineers[0], engineers[1], teams[1], engineers[3], engineers[8], teams[2], engineers[5], engineers[4], engineers[6], engineers[7]];

export function findByName(name: string): CatalogItem | undefined {
  return allItems.find(item => item.name === name);
}

export const filterTagNames = ['dotnet', 'react', 'security', 'testing', 'devops', 'api'];

export const homeStats = [
  { value: '1,842', label: 'engineers' },
  { value: '316', label: 'teams' },
  { value: '92,410', label: 'installs' },
];

export const featuredEngineers: CatalogItem[] = [engineers[2], engineers[0], engineers[8]];

export const engineerVersions: VersionInfo[] = [
  { version: 'v3.0.0', date: 'Aug 19, 2026', size: '48 KB', sha: '9f3ab1c2…e8d4' },
  { version: 'v2.2.0', date: 'Jul 28, 2026', size: '44 KB', sha: 'b7e02d91…77af' },
  { version: 'v2.1.0', date: 'Jul 2, 2026', size: '43 KB', sha: '4c8f77aa…0d19' },
];

export const teamVersions: VersionInfo[] = [
  { version: 'v1.2.0', date: 'Aug 3, 2026', size: '132 KB', sha: '4c8f77aa…b201' },
  { version: 'v1.1.0', date: 'Jun 20, 2026', size: '128 KB', sha: 'a19e3c50…f7d2' },
];

export const engineerMeta = [
  { key: 'Published', value: 'Mar 14, 2026' },
  { key: 'Last updated', value: 'Aug 19, 2026' },
  { key: 'Size', value: '48 KB' },
  { key: 'Versions', value: '7' },
  { key: 'sha256', value: '9f3ab1c2…e8d4' },
];

export const teamMeta = [
  { key: 'Published', value: 'May 2, 2026' },
  { key: 'Last updated', value: 'Aug 3, 2026' },
  { key: 'Members', value: '3 engineers' },
  { key: 'Size', value: '132 KB' },
  { key: 'sha256', value: '4c8f77aa…b201' },
];

export const squadMembers: TeamMemberInfo[] = [
  { emoji: '🧱', name: 'backend-engineer', author: 'mohamed-dive', pinnedVersion: 'v2.2.0' },
  { emoji: '⚛️', name: 'react-frontend', author: 'mohamed-dive', pinnedVersion: 'v2.4.1' },
  { emoji: '🧪', name: 'test-author', author: 'kenji-dev', pinnedVersion: 'v4.0.1' },
];

export const scanCategories = [
  { code: 'EXF', name: 'Data exfiltration' },
  { code: 'NET', name: 'Unvetted network calls' },
  { code: 'OBF', name: 'Obfuscated code' },
  { code: 'INJ', name: 'Prompt injection' },
  { code: 'SEC', name: 'Secret harvesting' },
];

export const faqEntries: FaqEntry[] = [
  { question: 'Is e3a free?', answer: 'Yes — browsing, installing and publishing are free. Limits (50 engineers, 10 teams per account) keep the catalog healthy.' },
  { question: 'Do I need an account to install?', answer: 'No. Installing only needs Claude Code — the marketplace is public. An account (GitHub sign-in) is only needed to publish.' },
  { question: 'What happens when an engineer I use publishes a new version?', answer: 'Nothing, until you choose. Installs track the latest version by default, but you can pin any version — and teams are always pinned snapshots.' },
  { question: 'Can I unpublish something?', answer: 'Yes, from your workspace. Existing installs keep working; the item just stops being discoverable and installable.' },
];

export const memberSearchPool: CatalogItem[] = [engineers[2], engineers[8], engineers[0], engineers[3]];

export function formatInstalls(installs: number): string {
  return `${installs >= 50 ? installs.toLocaleString('en-US') : installs} installs`;
}
