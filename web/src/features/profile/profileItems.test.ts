import { describe, expect, it } from 'vitest';
import type { CatalogEngineer, CreatorProfileTeam } from '../../lib/api';
import { formatTotalInstalls, joinedLabel, toEngineerItem, toTeamItem } from './profileItems';

function engineerWith(overrides: Partial<CatalogEngineer>): CatalogEngineer {
  return { id: 'e1', slug: 'dive-backend-engineer', displayName: 'Dive Backend Engineer', description: 'A .NET engineer.', tags: ['dotnet'], installCount: 12, latestVersionId: 'v1', createdAt: '2026-03-14T00:00:00Z', updatedAt: '2026-03-14T00:00:00Z', ...overrides };
}

function teamWith(overrides: Partial<CreatorProfileTeam>): CreatorProfileTeam {
  return { id: 't1', slug: 'dotnet-product-squad', displayName: 'Dotnet Product Squad', description: 'A squad.', tags: ['dotnet'], memberSlugs: ['a-engineer', 'b-engineer'], latestVersionId: 'v1', createdAt: '2026-03-14T00:00:00Z', updatedAt: '2026-03-14T00:00:00Z', ...overrides };
}

describe('toEngineerItem', () => {
  it('should carry the install count and slug onto the card item', () => {
    const item = toEngineerItem(engineerWith({ slug: 'sql-tuning-engineer', installCount: 41 }));

    expect(item.name).toBe('sql-tuning-engineer');
    expect(item.installs).toBe(41);
    expect(item.emoji.length).toBeGreaterThan(0);
  });

  it('should use an empty description when the engineer has none', () => {
    expect(toEngineerItem(engineerWith({ description: null })).description).toBe('');
  });
});

describe('toTeamItem', () => {
  it('should leave installs undefined so no install count is rendered', () => {
    const item = toTeamItem(teamWith({}));

    expect(item.installs).toBeUndefined();
    expect(item.team).toBe(true);
  });

  it('should map one member emoji per member slug', () => {
    const item = toTeamItem(teamWith({ memberSlugs: ['a-engineer', 'b-engineer', 'c-engineer'] }));

    expect(item.members).toHaveLength(3);
  });
});

describe('joinedLabel', () => {
  it('should format the date as month and year', () => {
    const label = joinedLabel('2026-03-14T00:00:00Z');

    expect(label).toContain('2026');
    expect(label).toContain('March');
  });

  it('should return an empty label when the date is missing or unparseable', () => {
    expect(joinedLabel(null)).toBe('');
    expect(joinedLabel('not-a-date')).toBe('');
  });
});

describe('formatTotalInstalls', () => {
  it('should group thousands with separators', () => {
    expect(formatTotalInstalls(12345)).toBe('12,345');
  });
});
