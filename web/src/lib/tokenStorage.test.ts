import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearToken, readToken, writeToken } from './tokenStorage';

function stubLocalStorage() {
  const entries = new Map<string, string>();
  const storage = {
    getItem: vi.fn((key: string) => entries.get(key) ?? null),
    setItem: vi.fn((key: string, value: string) => { entries.set(key, value); }),
    removeItem: vi.fn((key: string) => { entries.delete(key); }),
  };
  vi.stubGlobal('localStorage', storage);
  return storage;
}

let storage: ReturnType<typeof stubLocalStorage>;

beforeEach(() => { storage = stubLocalStorage(); });
afterEach(() => vi.unstubAllGlobals());

describe('tokenStorage', () => {
  it('should return null when no token is stored', () => {
    expect(readToken()).toBeNull();
  });

  it('should return the token that was written', () => {
    writeToken('jwt');

    expect(readToken()).toBe('jwt');
    expect(storage.setItem).toHaveBeenCalledWith('e3a.token', 'jwt');
  });

  it('should remove the token on clear', () => {
    writeToken('jwt');
    clearToken();

    expect(readToken()).toBeNull();
  });
});
