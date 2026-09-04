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
