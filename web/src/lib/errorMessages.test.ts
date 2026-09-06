import { describe, expect, it } from 'vitest';
import { GENERIC_ERROR_MESSAGE, messageForApiError, messageForErrorCode } from './errorMessages';
import { ApiError } from './http';

const callbackErrorCodes = [
  'AUTHENTICATION_CODE_MISSING',
  'AUTHENTICATION_STATE_INVALID',
  'AUTHENTICATION_STATE_EXPIRED',
  'GITHUB_TOKEN_EXCHANGE_FAILED',
  'GITHUB_PROFILE_FETCH_FAILED',
  'GITHUB_PROFILE_INVALID',
  'USER_NOT_AUTHENTICATED',
];

const teamErrorCodes = [
  'TEAM_NOT_FOUND',
  'TEAM_NOT_OWNED',
  'TEAM_LIMIT_REACHED',
  'TEAM_EMPTY',
  'TEAM_SLUG_FROZEN',
  'TEAM_SLUG_RESERVED',
  'TEAM_SLUG_INVALID',
  'TEAM_SLUG_TOO_SHORT',
  'TEAM_SLUG_TOO_LONG',
  'TEAM_DISPLAY_NAME_REQUIRED',
  'TEAM_DISPLAY_NAME_TOO_LONG',
  'TEAM_DISPLAY_NAME_INVALID',
  'TEAM_DESCRIPTION_TOO_LONG',
  'TEAM_TOO_MANY_TAGS',
  'TEAM_TAG_TOO_LONG',
  'TEAM_MEMBER_LIMIT_REACHED',
  'TEAM_MEMBER_DUPLICATE',
  'TEAM_MEMBER_NOT_PUBLISHED',
  'TEAM_MEMBER_VERSION_NOT_PUBLISHED',
  'TEAM_MEMBER_SNAPSHOT_EMPTY',
  'TEAM_MEMBER_MANIFEST_INVALID',
  'TEAM_ROSTER_INVALID',
  'PLUGIN_SECURITY_SCAN_BLOCKED',
  'MARKETPLACE_TEAM_LIMIT_EXCEEDED',
];

describe('messageForErrorCode', () => {
  it.each(callbackErrorCodes)('should map every callback error code to readable text', code => {
    const message = messageForErrorCode(code);

    expect(message.length).toBeGreaterThan(0);
    expect(message).not.toBe(code);
    expect(message).not.toContain('_');
  });

  it.each(teamErrorCodes)('should map every team error code to readable text', code => {
    const message = messageForErrorCode(code);

    expect(message.length).toBeGreaterThan(0);
    expect(message).not.toBe(code);
    expect(message).not.toContain('_');
    expect(message).not.toBe(GENERIC_ERROR_MESSAGE);
  });

  it('should return the generic message for an unknown code', () => {
    const message = messageForErrorCode('NOPE_NOT_REAL');

    expect(message).toBe(GENERIC_ERROR_MESSAGE);
    expect(message).not.toContain('NOPE_NOT_REAL');
  });

  it('should return the generic message for null', () => {
    expect(messageForErrorCode(null)).toBe(GENERIC_ERROR_MESSAGE);
  });
});

describe('messageForApiError', () => {
  it('should prefer the server message', () => {
    expect(messageForApiError(new ApiError(409, 'PUBLISH_ALREADY_IN_PROGRESS', 'A publish is already running.'))).toBe('A publish is already running.');
  });

  it('should fall back to the code map when the message is empty', () => {
    expect(messageForApiError(new ApiError(401, 'USER_NOT_AUTHENTICATED', ''))).toBe(messageForErrorCode('USER_NOT_AUTHENTICATED'));
  });

  it('should return the generic message for a non-ApiError', () => {
    expect(messageForApiError(new Error('boom'))).toBe(GENERIC_ERROR_MESSAGE);
  });
});
