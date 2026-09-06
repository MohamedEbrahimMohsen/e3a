import type { CatalogEngineer } from '../../lib/api';
import type { TeamMember, TeamMemberSelectionInput } from '../../lib/workspaceApi';

export interface TeamMemberDraft {
  engineerId: string;
  engineerSlug: string;
  pinnedVersionId: string;
  pinnedSemanticVersion: string | null;
}

export interface AddMemberOutcome {
  drafts: TeamMemberDraft[];
  problem: string | null;
}

export function toMemberDrafts(members: TeamMember[]): TeamMemberDraft[] {
  return [...members]
    .sort((left, right) => left.sortOrder - right.sortOrder || left.engineerId.localeCompare(right.engineerId))
    .map(member => ({
      engineerId: member.engineerId,
      engineerSlug: member.engineerSlug,
      pinnedVersionId: member.pinnedVersionId,
      pinnedSemanticVersion: member.pinnedSemanticVersion,
    }));
}

export function isMember(drafts: TeamMemberDraft[], engineerId: string): boolean {
  return drafts.some(draft => draft.engineerId === engineerId);
}

export function addMember(drafts: TeamMemberDraft[], engineer: CatalogEngineer, maxMembers: number): AddMemberOutcome {
  if (engineer.latestVersionId === null) {
    return { drafts, problem: `${engineer.slug} has no published version to pin yet.` };
  }
  if (isMember(drafts, engineer.id)) {
    return { drafts, problem: `${engineer.slug} is already on the roster.` };
  }
  if (drafts.length >= maxMembers) {
    return { drafts, problem: `A team can hold at most ${maxMembers} members.` };
  }
  const added: TeamMemberDraft = { engineerId: engineer.id, engineerSlug: engineer.slug, pinnedVersionId: engineer.latestVersionId, pinnedSemanticVersion: null };
  return { drafts: [...drafts, added], problem: null };
}

export function removeMemberDraft(drafts: TeamMemberDraft[], engineerId: string): TeamMemberDraft[] {
  return drafts.filter(draft => draft.engineerId !== engineerId);
}

export function moveMemberDraft(drafts: TeamMemberDraft[], fromIndex: number, toIndex: number): TeamMemberDraft[] {
  if (fromIndex === toIndex || fromIndex < 0 || toIndex < 0 || fromIndex >= drafts.length || toIndex >= drafts.length) {
    return drafts;
  }
  const reordered = [...drafts];
  const [moved] = reordered.splice(fromIndex, 1);
  reordered.splice(toIndex, 0, moved);
  return reordered;
}

export function toMemberSelections(drafts: TeamMemberDraft[]): TeamMemberSelectionInput[] {
  return drafts.map(draft => ({ engineerId: draft.engineerId, pinnedVersionId: draft.pinnedVersionId }));
}

export function memberVersionLabel(draft: TeamMemberDraft): string {
  return draft.pinnedSemanticVersion === null ? 'latest' : `v${draft.pinnedSemanticVersion}`;
}

export function teamStructurePaths(drafts: TeamMemberDraft[]): string[] {
  if (drafts.length === 0) {
    return [];
  }
  return ['.claude-plugin/plugin.json', 'agents/', 'commands/', ...drafts.map(draft => `skills/${draft.engineerSlug}--*/`)];
}
