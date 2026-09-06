import { describe, expect, it } from 'vitest';
import { publishTargetFor } from './publishTarget';

describe('publishTargetFor', () => {
  it('should route a team publish back to the team composer', () => {
    const target = publishTargetFor('Team', 'team-7');

    expect(target.itemType).toBe('Team');
    expect(target.composerPath).toBe('/workspace/teams/team-7');
    expect(target.noun).toBe('team');
  });

  it('should route an engineer publish back to the engineer composer', () => {
    const target = publishTargetFor('Engineer', 'engineer-7');

    expect(target.itemType).toBe('Engineer');
    expect(target.composerPath).toBe('/workspace/engineers/engineer-7');
    expect(target.noun).toBe('engineer');
  });

  it('should treat an unrecognised item type as an engineer', () => {
    expect(publishTargetFor('Squad', 'item-7').itemType).toBe('Engineer');
  });
});
