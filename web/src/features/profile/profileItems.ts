import { emojiFor } from '../../lib/api';
import type { CatalogEngineer, CreatorProfileTeam } from '../../lib/api';
import type { CatalogItem } from '../../lib/types';

export function toEngineerItem(engineer: CatalogEngineer): CatalogItem {
  return { emoji: emojiFor(engineer.slug), name: engineer.slug, description: engineer.description ?? '', tags: engineer.tags, installs: engineer.installCount };
}

export function toTeamItem(team: CreatorProfileTeam): CatalogItem {
  return { emoji: emojiFor(team.slug), name: team.slug, description: team.description ?? '', tags: team.tags, team: true, members: team.memberSlugs.map(emojiFor) };
}

export function joinedLabel(createdAt: string | null | undefined): string {
  if (!createdAt) {
    return '';
  }
  const joined = new Date(createdAt);
  return Number.isNaN(joined.getTime()) ? '' : joined.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
}

export function formatTotalInstalls(totalInstalls: number): string {
  return totalInstalls.toLocaleString('en-US');
}
