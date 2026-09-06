import type { Engineer, Team } from '../../lib/workspaceApi';

const NO_INSTALL_COUNT_LABEL = '—';

export type WorkspaceRowType = 'Engineer' | 'Team';

export interface WorkspaceRow {
  itemId: string;
  slug: string;
  displayName: string;
  type: WorkspaceRowType;
  status: string;
  installCount: number | null;
  updatedAt: string;
  editPath: string;
  actionPath: string;
  actionLabel: 'Publish' | 'View status';
  viewPath: string;
}

function actionPathFor(latestVersionId: string | null, editPath: string): string {
  return latestVersionId ? `/workspace/publish?versionId=${latestVersionId}` : editPath;
}

function toEngineerRow(engineer: Engineer): WorkspaceRow {
  const editPath = `/workspace/engineers/${engineer.id}`;
  return {
    itemId: engineer.id,
    slug: engineer.slug,
    displayName: engineer.displayName,
    type: 'Engineer',
    status: engineer.status,
    installCount: engineer.installCount,
    updatedAt: engineer.updatedAt,
    editPath,
    actionPath: actionPathFor(engineer.latestVersionId, editPath),
    actionLabel: engineer.latestVersionId ? 'View status' : 'Publish',
    viewPath: engineer.status === 'Published' ? `/e/${engineer.slug}` : editPath,
  };
}

function toTeamRow(team: Team): WorkspaceRow {
  const editPath = `/workspace/teams/${team.id}`;
  return {
    itemId: team.id,
    slug: team.slug,
    displayName: team.displayName,
    type: 'Team',
    status: team.status,
    installCount: null,
    updatedAt: team.updatedAt,
    editPath,
    actionPath: actionPathFor(team.latestVersionId, editPath),
    actionLabel: team.latestVersionId ? 'View status' : 'Publish',
    viewPath: editPath,
  };
}

export function toWorkspaceRows(engineers: Engineer[], teams: Team[]): WorkspaceRow[] {
  return [...engineers.map(toEngineerRow), ...teams.map(toTeamRow)]
    .sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt) || left.itemId.localeCompare(right.itemId));
}

export function formatInstallCount(installCount: number | null): string {
  return installCount === null ? NO_INSTALL_COUNT_LABEL : installCount.toLocaleString('en-US');
}

export function formatUpdated(updatedAt: string): string {
  return new Date(updatedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}
