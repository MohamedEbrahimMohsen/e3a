import { describe, expect, it } from 'vitest';
import type { CatalogEngineer } from '../../lib/api';
import type { TeamMember } from '../../lib/workspaceApi';
import { addMember, isMember, memberVersionLabel, moveMemberDraft, removeMemberDraft, teamStructurePaths, toMemberDrafts, toMemberSelections, type TeamMemberDraft } from './teamMembers';

const MAX_MEMBERS = 10;

function engineerWith(overrides: Partial<CatalogEngineer>): CatalogEngineer {
  return { id: 'engineer-1', slug: 'payments-engineer', displayName: 'Payments Engineer', description: 'Handles payments.', tags: ['payments'], installCount: 12, latestVersionId: 'version-1', createdAt: '2026-03-14T00:00:00Z', updatedAt: '2026-03-14T00:00:00Z', ...overrides };
}

function memberWith(overrides: Partial<TeamMember>): TeamMember {
  return { engineerId: 'engineer-1', engineerSlug: 'payments-engineer', pinnedVersionId: 'version-1', pinnedSemanticVersion: '1.0.0', sortOrder: 0, ...overrides };
}

function draft(slug: string, versionId: string, semanticVersion: string | null): TeamMemberDraft {
  return { engineerId: `${slug}-id`, engineerSlug: slug, pinnedVersionId: versionId, pinnedSemanticVersion: semanticVersion };
}

const rosterOfThree = [draft('a-engineer', 'version-a', '1.0.0'), draft('b-engineer', 'version-b', '2.0.0'), draft('c-engineer', 'version-c', '3.0.0')];

describe('addMember', () => {
  it('should add the engineer pinned to its latest version when the roster has room', () => {
    const outcome = addMember([draft('a-engineer', 'version-a', '1.0.0')], engineerWith({ id: 'engineer-9', latestVersionId: 'version-9' }), MAX_MEMBERS);

    expect(outcome.drafts).toHaveLength(2);
    expect(outcome.drafts[1].pinnedVersionId).toBe('version-9');
    expect(outcome.drafts[1].pinnedSemanticVersion).toBeNull();
    expect(outcome.problem).toBeNull();
  });

  it('should refuse an engineer with no published version', () => {
    const drafts: TeamMemberDraft[] = [];

    const outcome = addMember(drafts, engineerWith({ slug: 'unpublished-engineer', latestVersionId: null }), MAX_MEMBERS);

    expect(outcome.problem).toContain('unpublished-engineer');
    expect(outcome.drafts).toBe(drafts);
  });

  it('should refuse an engineer already on the roster', () => {
    const outcome = addMember([draft('payments-engineer', 'version-1', '1.0.0')], engineerWith({ id: 'payments-engineer-id' }), MAX_MEMBERS);

    expect(outcome.problem).not.toBeNull();
    expect(outcome.drafts).toHaveLength(1);
  });

  it('should refuse a new member when the roster is at the cap', () => {
    const drafts = [draft('a-engineer', 'version-a', '1.0.0'), draft('b-engineer', 'version-b', '2.0.0')];

    const outcome = addMember(drafts, engineerWith({ id: 'engineer-9', slug: 'c-engineer' }), 2);

    expect(outcome.drafts).toHaveLength(2);
    expect(outcome.problem).toContain('2');
  });
});

describe('isMember', () => {
  it('should report true for an engineer on the roster', () => {
    expect(isMember(rosterOfThree, 'b-engineer-id')).toBe(true);
  });

  it('should report false for an engineer not on the roster', () => {
    expect(isMember(rosterOfThree, 'z-engineer-id')).toBe(false);
  });
});

describe('removeMemberDraft', () => {
  it('should drop only the named member', () => {
    const remaining = removeMemberDraft(rosterOfThree, 'b-engineer-id');

    expect(remaining).toHaveLength(2);
    expect(remaining.map(member => member.engineerSlug)).toEqual(['a-engineer', 'c-engineer']);
  });

  it('should leave the roster unchanged when the engineer is not on it', () => {
    const remaining = removeMemberDraft(rosterOfThree, 'z-engineer-id');

    expect(remaining).toHaveLength(3);
    expect(remaining.map(member => member.engineerSlug)).toEqual(['a-engineer', 'b-engineer', 'c-engineer']);
  });
});

describe('moveMemberDraft', () => {
  it('should move a member to an earlier index and shift the others down', () => {
    const reordered = moveMemberDraft(rosterOfThree, 2, 0);

    expect(reordered.map(member => member.engineerSlug)).toEqual(['c-engineer', 'a-engineer', 'b-engineer']);
  });

  it('should move a member to a later index', () => {
    const reordered = moveMemberDraft(rosterOfThree, 0, 2);

    expect(reordered.map(member => member.engineerSlug)).toEqual(['b-engineer', 'c-engineer', 'a-engineer']);
  });

  it('should leave the roster unchanged when an index is out of range', () => {
    expect(moveMemberDraft(rosterOfThree, 0, -1).map(member => member.engineerSlug)).toEqual(['a-engineer', 'b-engineer', 'c-engineer']);
    expect(moveMemberDraft(rosterOfThree, 0, rosterOfThree.length).map(member => member.engineerSlug)).toEqual(['a-engineer', 'b-engineer', 'c-engineer']);
  });
});

describe('toMemberDrafts', () => {
  it('should order members by sortOrder rather than array order', () => {
    const drafts = toMemberDrafts([
      memberWith({ engineerId: 'third-id', engineerSlug: 'c-engineer', sortOrder: 2 }),
      memberWith({ engineerId: 'first-id', engineerSlug: 'a-engineer', sortOrder: 0 }),
      memberWith({ engineerId: 'second-id', engineerSlug: 'b-engineer', sortOrder: 1 }),
    ]);

    expect(drafts.map(member => member.engineerSlug)).toEqual(['a-engineer', 'b-engineer', 'c-engineer']);
  });

  it('should carry the pinned version and semantic version onto each draft', () => {
    const drafts = toMemberDrafts([memberWith({ pinnedVersionId: 'version-7', pinnedSemanticVersion: '2.3.1' })]);

    expect(drafts[0].pinnedVersionId).toBe('version-7');
    expect(drafts[0].pinnedSemanticVersion).toBe('2.3.1');
  });
});

describe('toMemberSelections', () => {
  it('should send each member\'s explicit pinned version id in roster order', () => {
    const selections = toMemberSelections(rosterOfThree);

    expect(selections.map(selection => selection.pinnedVersionId)).toEqual(['version-a', 'version-b', 'version-c']);
    expect(selections.map(selection => selection.engineerId)).toEqual(['a-engineer-id', 'b-engineer-id', 'c-engineer-id']);
  });
});

describe('memberVersionLabel', () => {
  it('should prefix a saved semantic version with v', () => {
    expect(memberVersionLabel(draft('a-engineer', 'version-a', '1.2.0'))).toBe('v1.2.0');
  });

  it('should read latest when the member has not been saved yet', () => {
    expect(memberVersionLabel(draft('a-engineer', 'version-a', null))).toBe('latest');
  });
});

describe('teamStructurePaths', () => {
  it('should namespace each member\'s skills folder with a double hyphen', () => {
    const paths = teamStructurePaths([draft('payments-engineer', 'version-a', '1.0.0'), draft('react-frontend', 'version-b', '2.0.0')]);

    expect(paths).toContain('skills/payments-engineer--*/');
    expect(paths.filter(path => path.startsWith('skills/'))).toHaveLength(2);
  });

  it('should return no paths for an empty roster', () => {
    expect(teamStructurePaths([])).toEqual([]);
  });
});
