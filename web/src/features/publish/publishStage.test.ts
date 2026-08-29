import { describe, expect, it } from 'vitest';
import { GENERIC_ERROR_MESSAGE } from '../../lib/errorMessages';
import { failureText, isFailedStatus, isTerminalStatus, stepIndexFor } from './publishStage';

describe('stepIndexFor', () => {
  it('should map each pipeline status to its step', () => {
    expect(stepIndexFor('Queued')).toBe(0);
    expect(stepIndexFor('Building')).toBe(1);
    expect(stepIndexFor('Published')).toBe(2);
  });

  it('should return -1 for an unknown status', () => {
    expect(stepIndexFor('Nonsense')).toBe(-1);
  });
});

describe('isTerminalStatus', () => {
  it('should be true for finished statuses', () => {
    expect(isTerminalStatus('Published')).toBe(true);
    expect(isTerminalStatus('Rejected')).toBe(true);
    expect(isTerminalStatus('Failed')).toBe(true);
  });

  it('should be false while the job is running', () => {
    expect(isTerminalStatus('Queued')).toBe(false);
    expect(isTerminalStatus('Building')).toBe(false);
  });
});

describe('isFailedStatus', () => {
  it('should be true only for rejected and failed', () => {
    expect(isFailedStatus('Rejected')).toBe(true);
    expect(isFailedStatus('Failed')).toBe(true);
    expect(isFailedStatus('Published')).toBe(false);
    expect(isFailedStatus('Queued')).toBe(false);
    expect(isFailedStatus('Building')).toBe(false);
  });
});

describe('failureText', () => {
  const screamingSnake = /[A-Z0-9]+_[A-Z0-9_]*/;

  it('should never render a raw error code, for a single code or several joined ones', () => {
    expect(failureText('PLUGIN_NO_INSTALLABLE_CONTENT')).not.toMatch(screamingSnake);
    expect(failureText('ENGINEER_SNAPSHOT_EMPTY')).not.toMatch(screamingSnake);
    expect(failureText('PLUGIN_UNSAFE_PATH, PLUGIN_TOO_MANY_FILES, PLUGIN_TOO_LARGE')).not.toMatch(screamingSnake);
    expect(failureText('PLUGIN_NOT_A_REAL_CODE')).toBe(GENERIC_ERROR_MESSAGE);
  });

  it('should join every code in a comma-separated reason into readable text', () => {
    const text = failureText('PLUGIN_UNSAFE_PATH, PLUGIN_TOO_LARGE');

    expect(text).toBe('The plugin contains an unsafe file path. The plugin is larger than the allowed size.');
  });

  it('should pass a prose reason through unchanged', () => {
    expect(failureText('The build host ran out of disk.')).toBe('The build host ran out of disk.');
  });

  it('should return the generic message for a null or empty reason', () => {
    expect(failureText(null)).toBe(GENERIC_ERROR_MESSAGE);
    expect(failureText('  ')).toBe(GENERIC_ERROR_MESSAGE);
  });
});
