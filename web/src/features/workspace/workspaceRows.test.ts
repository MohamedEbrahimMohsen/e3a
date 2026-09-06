import { describe, expect, it } from 'vitest';
import type { Engineer, Team } from '../../lib/workspaceApi';
import { formatInstallCount, formatUpdated, toWorkspaceRows } from './workspaceRows';

function engineerWith(overrides: Partial<Engineer>): Engineer {
  return { id: 'engineer-1', slug: 'payments-engineer', displayName: 'Payments Engineer', description: null, tags: [], status: 'Draft', latestVersionId: null, installCount: 12, createdAt: '2026-03-14T00:00:00Z', updatedAt: '2026-03-14T00:00:00Z', ...overrides };
}

function teamWith(overrides: Partial<Team>): Team {
  return { id: 'team-1', slug: 'fintech-launch-crew', displayName: 'Fintech Launch Crew', description: null, tags: [], status: 'Draft', latestVersionId: null, memberCount: 2, createdAt: '2026-03-14T00:00:00Z', updatedAt: '2026-03-14T00:00:00Z', ...overrides };
}

describe('toWorkspaceRows', () => {
  it('should type an engineer row Engineer and a team row Team', () => {
    const rows = toWorkspaceRows([engineerWith({})], [teamWith({})]);

    expect(rows.filter(row => row.type === 'Engineer')).toHaveLength(1);
    expect(rows.filter(row => row.type === 'Team')).toHaveLength(1);
  });

  it('should order rows by updatedAt descending across both item types', () => {
    const rows = toWorkspaceRows(
      [engineerWith({ id: 'engineer-1', updatedAt: '2026-03-01T00:00:00Z' })],
      [teamWith({ id: 'team-1', updatedAt: '2026-05-20T00:00:00Z' })],
    );

    expect(rows.map(row => row.itemId)).toEqual(['team-1', 'engineer-1']);
  });

  it('should leave a team install count null so no number is rendered', () => {
    const rows = toWorkspaceRows([engineerWith({ installCount: 41 })], [teamWith({})]);

    expect(rows.find(row => row.type === 'Team')?.installCount).toBeNull();
    expect(rows.find(row => row.type === 'Engineer')?.installCount).toBe(41);
  });

  it('should point an engineer edit link at the engineer composer and a team edit link at the team composer', () => {
    const rows = toWorkspaceRows([engineerWith({ id: 'engineer-7' })], [teamWith({ id: 'team-7' })]);

    expect(rows.find(row => row.type === 'Engineer')?.editPath).toBe('/workspace/engineers/engineer-7');
    expect(rows.find(row => row.type === 'Team')?.editPath).toBe('/workspace/teams/team-7');
  });

  it('should label the action View status when a latest version exists and Publish when it does not', () => {
    const rows = toWorkspaceRows(
      [engineerWith({ id: 'engineer-a', latestVersionId: 'version-3', updatedAt: '2026-05-20T00:00:00Z' }), engineerWith({ id: 'engineer-b', latestVersionId: null, updatedAt: '2026-03-01T00:00:00Z' })],
      [],
    );

    expect(rows[0].actionLabel).toBe('View status');
    expect(rows[0].actionPath).toBe('/workspace/publish?versionId=version-3');
    expect(rows[1].actionLabel).toBe('Publish');
    expect(rows[1].actionPath).toBe('/workspace/engineers/engineer-b');
  });

  it('should point a published engineer view link at its catalog page and a draft one at its composer', () => {
    const rows = toWorkspaceRows(
      [engineerWith({ id: 'engineer-a', slug: 'payments-engineer', status: 'Published', updatedAt: '2026-05-20T00:00:00Z' }), engineerWith({ id: 'engineer-b', status: 'Draft', updatedAt: '2026-03-01T00:00:00Z' })],
      [],
    );

    expect(rows[0].viewPath).toBe('/e/payments-engineer');
    expect(rows[1].viewPath).toBe('/workspace/engineers/engineer-b');
  });

  it('should point a team view link at the composer because no public team page exists', () => {
    const rows = toWorkspaceRows([], [teamWith({ id: 'team-7', slug: 'fintech-launch-crew', status: 'Published' })]);

    expect(rows[0].viewPath).toBe(rows[0].editPath);
    expect(rows[0].viewPath.startsWith('/t/')).toBe(false);
  });

  it('should return no rows when the creator has neither engineers nor teams', () => {
    expect(toWorkspaceRows([], [])).toEqual([]);
  });
});

describe('formatInstallCount', () => {
  it('should group thousands with separators', () => {
    expect(formatInstallCount(12345)).toBe('12,345');
  });

  it('should render an em dash when there is no install count', () => {
    expect(formatInstallCount(null)).toBe('—');
  });
});

describe('formatUpdated', () => {
  it('should format the date as short month, day and year', () => {
    const label = formatUpdated('2026-03-14T12:00:00Z');

    expect(label).toContain('2026');
    expect(label).toContain('Mar');
  });
});
