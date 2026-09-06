import type { PluginItemType } from '../../lib/config';

export interface PublishTarget {
  itemType: PluginItemType;
  composerPath: string;
  noun: string;
}

export function publishTargetFor(itemType: string, itemId: string): PublishTarget {
  return itemType === 'Team'
    ? { itemType: 'Team', composerPath: `/workspace/teams/${itemId}`, noun: 'team' }
    : { itemType: 'Engineer', composerPath: `/workspace/engineers/${itemId}`, noun: 'engineer' };
}
