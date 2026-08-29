import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { config } from './config';
import { ApiError, requestJson, setUnauthorizedHandler } from './http';

function stubLocalStorage(token: string | null) {
  const entries = new Map<string, string>();
  if (token) {
    entries.set('e3a.token', token);
  }
  const storage = {
    getItem: vi.fn((key: string) => entries.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => { entries.set(key, value); }),
    removeItem: vi.fn((key: string) => { entries.delete(key); }),
  };
  vi.stubGlobal('localStorage', storage);
  return storage;
}

function stubFetch(response: Partial<Response>) {
  const fetchMock = vi.fn(async (_url: string, _init: RequestInit) => response as Response);
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function headersOf(fetchMock: ReturnType<typeof stubFetch>): Record<string, string> {
  return fetchMock.mock.calls[0][1].headers as Record<string, string>;
}

function initOf(fetchMock: ReturnType<typeof stubFetch>): RequestInit {
  return fetchMock.mock.calls[0][1];
}

const okResponse = { ok: true, status: 200, json: async () => ({}) };

beforeEach(() => setUnauthorizedHandler(null));
afterEach(() => { vi.unstubAllGlobals(); setUnauthorizedHandler(null); });

describe('requestJson', () => {
  it('should attach the bearer token when one is stored', async () => {
    stubLocalStorage('jwt');
    const fetchMock = stubFetch(okResponse);

    await requestJson('/auth/me');

    expect(headersOf(fetchMock).Authorization).toBe('Bearer jwt');
  });

  it('should send no authorization header when signed out', async () => {
    stubLocalStorage(null);
    const fetchMock = stubFetch(okResponse);

    await requestJson('/catalog');

    expect('Authorization' in headersOf(fetchMock)).toBe(false);
  });

  it('should prefix the path with the configured API base URL', async () => {
    stubLocalStorage(null);
    const fetchMock = stubFetch(okResponse);

    await requestJson('/auth/me');

    expect(fetchMock.mock.calls[0][0]).toBe(`${config.apiBaseUrl}/auth/me`);
  });

  it('should serialize a json body and set the content type', async () => {
    stubLocalStorage(null);
    const fetchMock = stubFetch(okResponse);

    await requestJson('/engineers/1/publish', { method: 'POST', body: { increment: 'Patch' } });

    expect(initOf(fetchMock).body).toBe('{"increment":"Patch"}');
    expect(headersOf(fetchMock)['Content-Type']).toBe('application/json');
  });

  it('should send form data without a content type header', async () => {
    stubLocalStorage(null);
    const fetchMock = stubFetch(okResponse);
    const formData = new FormData();

    await requestJson('/engineers/1/upload', { method: 'POST', formData });

    expect(initOf(fetchMock).body).toBe(formData);
    expect('Content-Type' in headersOf(fetchMock)).toBe(false);
  });

  it('should clear the token and notify the handler on 401', async () => {
    const storage = stubLocalStorage('jwt');
    stubFetch({ ok: false, status: 401, json: async () => { throw new Error('no body'); } });
    const handler = vi.fn();
    setUnauthorizedHandler(handler);

    await expect(requestJson('/auth/me')).rejects.toMatchObject({ status: 401 });
    expect(storage.removeItem).toHaveBeenCalledWith('e3a.token');
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('should carry the server code and message on a failed response', async () => {
    stubLocalStorage(null);
    stubFetch({ ok: false, status: 409, json: async () => ({ code: 'X', message: 'Y' }) });

    const error = (await requestJson('/engineers').catch(caught => caught)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe('X');
    expect(error.message).toBe('Y');
  });

  it('should survive a non-json error body', async () => {
    stubLocalStorage(null);
    stubFetch({ ok: false, status: 500, json: async () => { throw new Error('no body'); } });

    const error = (await requestJson('/engineers').catch(caught => caught)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBeNull();
    expect(error.message).toContain('500');
  });

  it('should not call the unauthorized handler on a 403', async () => {
    const storage = stubLocalStorage('jwt');
    stubFetch({ ok: false, status: 403, json: async () => { throw new Error('no body'); } });
    const handler = vi.fn();
    setUnauthorizedHandler(handler);

    await expect(requestJson('/engineers')).rejects.toBeInstanceOf(ApiError);
    expect(handler).not.toHaveBeenCalled();
    expect(storage.removeItem).not.toHaveBeenCalled();
  });

  it('should return the parsed body on success', async () => {
    stubLocalStorage(null);
    stubFetch({ ok: true, status: 200, json: async () => ({ id: '1' }) });

    const result = await requestJson('/engineers/1');

    expect(result).toEqual({ id: '1' });
  });
});
