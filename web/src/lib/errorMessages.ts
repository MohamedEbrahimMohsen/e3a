import { ApiError } from './http';

export const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';

const errorMessages: Record<string, string> = {
  AUTHENTICATION_CODE_MISSING: 'GitHub did not send an authorization code. Please try signing in again.',
  AUTHENTICATION_STATE_INVALID: 'We could not verify that sign-in request. Please try again.',
  AUTHENTICATION_STATE_EXPIRED: 'That sign-in request expired. Please try again.',
  GITHUB_TOKEN_EXCHANGE_FAILED: 'We could not complete the sign-in with GitHub. Please try again.',
  GITHUB_PROFILE_FETCH_FAILED: 'We could not read your GitHub profile. Please try again.',
  GITHUB_PROFILE_INVALID: 'Your GitHub profile is missing details we need (a login and an id).',
  USER_NOT_AUTHENTICATED: 'Your session has ended. Please sign in again.',
  USER_NOT_FOUND: "We couldn't find that account.",
  CATALOG_CREATOR_LOGIN_TOO_LONG: 'That creator login is longer than we allow.',
  ENGINEER_NOT_FOUND: "We couldn't find that engineer.",
  ENGINEER_DRAFT_NOT_UPLOADED: 'No draft has been uploaded for this engineer yet.',
  ENGINEER_SNAPSHOT_EMPTY: 'The uploaded draft has no files to publish.',
  PLUGIN_MANIFEST_ASSET_MISSING: 'A file listed in the import manifest is missing from the upload.',
  PLUGIN_NO_INSTALLABLE_CONTENT: 'The plugin has no agents, skills or commands to install.',
  PLUGIN_UNSAFE_PATH: 'The plugin contains an unsafe file path.',
  PLUGIN_SKILL_MISSING_SKILL_FILE: 'A skill folder is missing its SKILL.md file.',
  PLUGIN_TOO_MANY_FILES: 'The plugin contains too many files.',
  PLUGIN_TOO_LARGE: 'The plugin is larger than the allowed size.',
  PLUGIN_DUPLICATE_PATH: 'The plugin contains two files with the same path.',
  PLUGIN_SECURITY_SCAN_BLOCKED: 'The security scan blocked this publish. Review the report and try again.',
  MARKETPLACE_TEAM_LIMIT_EXCEEDED: 'The marketplace is at its team limit. Please try again later.',
  TEAM_NOT_FOUND: "We couldn't find that team.",
  TEAM_NOT_OWNED: 'That team belongs to someone else.',
  TEAM_LIMIT_REACHED: 'You have reached the number of teams you can create.',
  TEAM_EMPTY: 'Add at least one member before publishing this team.',
  TEAM_SLUG_FROZEN: "A team's name cannot change after its first publish.",
  TEAM_SLUG_RESERVED: 'That team name is reserved. Please choose another.',
  TEAM_SLUG_INVALID: 'That team name cannot be turned into a valid plugin name.',
  TEAM_SLUG_TOO_SHORT: 'That team name is too short.',
  TEAM_SLUG_TOO_LONG: 'That team name is too long.',
  TEAM_DISPLAY_NAME_REQUIRED: 'Please give the team a name.',
  TEAM_DISPLAY_NAME_TOO_LONG: 'That team name is longer than we allow.',
  TEAM_DISPLAY_NAME_INVALID: 'A team name needs at least one English letter or digit.',
  TEAM_DESCRIPTION_TOO_LONG: 'That description is too long. Please shorten it.',
  TEAM_TOO_MANY_TAGS: 'That is more tags than a team can carry.',
  TEAM_TAG_TOO_LONG: 'One of those tags is too long.',
  TEAM_MEMBER_LIMIT_REACHED: 'That is more members than a team can hold.',
  TEAM_MEMBER_DUPLICATE: 'That engineer is already on the roster.',
  TEAM_MEMBER_NOT_PUBLISHED: 'That engineer has no published version to pin.',
  TEAM_MEMBER_VERSION_NOT_PUBLISHED: 'The version pinned for one member is not published.',
  TEAM_MEMBER_SNAPSHOT_EMPTY: "One member's published files could not be read.",
  TEAM_MEMBER_MANIFEST_INVALID: "One member's published manifest could not be read.",
  TEAM_ROSTER_INVALID: "This team's saved roster could not be read.",
  REPORT_ITEM_ID_REQUIRED: 'Please choose the item you are reporting.',
  REPORT_ITEM_TYPE_INVALID: 'That item cannot be reported.',
  REPORT_REASON_INVALID: 'Please choose a reason from the list.',
  REPORT_DETAILS_REQUIRED: 'Please describe what you found.',
  REPORT_DETAILS_TOO_LONG: 'Those details are too long. Please shorten them.',
  REPORT_ITEM_NOT_FOUND: 'We could not find the item you are reporting.',
  REPORT_LIMIT_REACHED: 'This item has already been reported enough times for us to review it. Thank you.',
};

export function messageForErrorCode(code: string | null | undefined): string {
  if (!code) {
    return GENERIC_ERROR_MESSAGE;
  }
  return errorMessages[code] ?? GENERIC_ERROR_MESSAGE;
}

export function messageForApiError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return GENERIC_ERROR_MESSAGE;
  }
  const serverMessage = error.message.trim();
  return serverMessage.length > 0 ? serverMessage : messageForErrorCode(error.code);
}
