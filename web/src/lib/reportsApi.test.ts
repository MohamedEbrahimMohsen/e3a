import { afterEach, describe, expect, it, vi } from 'vitest';
import { config } from './config';
import { ApiError } from './http';
import { REPORT_REASON_OPTIONS, canSubmitReport, normalizeReportDetails, submitReport } from './reportsApi';

function stubLocalStorage(token: string | null) {
  const entries = new Map<string, string>();
  if (token) {
    entries.set('e3a.token', token);
  }
  vi.stubGlobal('localStorage', {
    getItem: vi.fn((key: string) => entries.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => { entries.set(key, value); }),
    removeItem: vi.fn((key: string) => { entries.delete(key); }),
  });
}

function stubFetch(response: Partial<Response>) {
  const fetchMock = vi.fn(async (_url: string, _init: RequestInit) => response as Response);
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function headersOf(fetchMock: ReturnType<typeof stubFetch>): Record<string, string> {
  return fetchMock.mock.calls[0][1].headers as Record<string, string>;
}

const itemId = '5f2f6d0c-2b1a-4f3e-9a77-0c2f4f5a1b33';
const submittedResponse = { ok: true, status: 200, json: async () => ({ id: 'report-id', status: 'Open', createdAt: '2026-09-05T10:00:00+00:00' }) };

function submitMaliciousReport() {
  return submitReport({ itemType: 'Engineer', itemId, reason: 'Malicious', details: 'It exfiltrates credentials.' });
}

afterEach(() => vi.unstubAllGlobals());

describe('submitReport', () => {
  it('should POST to the reports endpoint', async () => {
    stubLocalStorage(null);
    const fetchMock = stubFetch(submittedResponse);

    await submitMaliciousReport();

    expect(fetchMock.mock.calls[0][0]).toBe(`${config.apiBaseUrl}/reports`);
    expect(fetchMock.mock.calls[0][1].method).toBe('POST');
  });

  it('should send the item identity and reason in the request body', async () => {
    stubLocalStorage(null);
    const fetchMock = stubFetch(submittedResponse);

    await submitMaliciousReport();

    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ itemType: 'Engineer', itemId, reason: 'Malicious', details: 'It exfiltrates credentials.' });
    expect(headersOf(fetchMock)['Content-Type']).toBe('application/json');
  });

  it('should send no authorization header when the reporter is signed out', async () => {
    stubLocalStorage(null);
    const fetchMock = stubFetch(submittedResponse);

    await submitMaliciousReport();

    expect('Authorization' in headersOf(fetchMock)).toBe(false);
  });

  it('should attach the bearer token when the reporter is signed in', async () => {
    stubLocalStorage('jwt');
    const fetchMock = stubFetch(submittedResponse);

    await submitMaliciousReport();

    expect(headersOf(fetchMock).Authorization).toBe('Bearer jwt');
  });

  it('should reject with an ApiError when the API refuses the report', async () => {
    stubLocalStorage(null);
    stubFetch({ ok: false, status: 429, json: async () => ({ code: 'REPORT_LIMIT_REACHED', message: 'This item has already been reported 20 times.' }) });

    const error = (await submitMaliciousReport().catch(caught => caught)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe('REPORT_LIMIT_REACHED');
  });
});

describe('normalizeReportDetails', () => {
  it('should return null when the details are only whitespace', () => {
    expect(normalizeReportDetails('   ')).toBeNull();
    expect(normalizeReportDetails('')).toBeNull();
  });

  it('should trim the surrounding whitespace when details are present', () => {
    expect(normalizeReportDetails('  hook posts my keys  ')).toBe('hook posts my keys');
  });
});

describe('canSubmitReport', () => {
  it('should allow submission when the reason is not Other and details are empty', () => {
    expect(canSubmitReport('Malicious', '')).toBe(true);
  });

  it('should block submission when the reason is Other and details are empty', () => {
    expect(canSubmitReport('Other', '   ')).toBe(false);
  });

  it('should allow submission when the reason is Other and details are provided', () => {
    expect(canSubmitReport('Other', 'It ships a fork bomb')).toBe(true);
  });
});

describe('REPORT_REASON_OPTIONS', () => {
  it('should list every reason the API accepts', () => {
    expect(REPORT_REASON_OPTIONS.map(option => option.value)).toEqual(['Malicious', 'Spam', 'Copyright', 'Other']);
  });
});
