import { GENERIC_ERROR_MESSAGE, messageForErrorCode } from '../../lib/errorMessages';

export const PUBLISH_STEP_LABELS = ['Queued', 'Building', 'Published'] as const;

export function stepIndexFor(status: string): number {
  switch (status) {
    case 'Queued': return 0;
    case 'Building': return 1;
    case 'Published': return 2;
    default: return -1;
  }
}

export function isTerminalStatus(status: string): boolean {
  switch (status) {
    case 'Published':
    case 'Rejected':
    case 'Failed': return true;
    default: return false;
  }
}

export function isFailedStatus(status: string): boolean {
  switch (status) {
    case 'Rejected':
    case 'Failed': return true;
    default: return false;
  }
}

export function failureText(failureReason: string | null | undefined): string {
  const parts = (failureReason ?? '')
    .split(',')
    .map(part => part.trim())
    .filter(part => part.length > 0)
    .map(part => (/^[A-Z0-9_]+$/.test(part) ? messageForErrorCode(part) : part));
  return parts.length > 0 ? parts.join(' ') : GENERIC_ERROR_MESSAGE;
}
