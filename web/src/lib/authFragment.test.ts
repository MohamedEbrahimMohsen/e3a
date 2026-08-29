import { afterEach, describe, expect, it, vi } from 'vitest';
import { clearAuthFragment, parseAuthFragment } from './authFragment';

afterEach(() => vi.unstubAllGlobals());

describe('parseAuthFragment', () => {
  it('should return the token when the fragment carries one', () => {
    expect(parseAuthFragment('#token=abc.def')).toEqual({ token: 'abc.def', errorCode: null });
  });

  it('should decode a percent-encoded token', () => {
    expect(parseAuthFragment('#token=a%2Bb%3Dc').token).toBe('a+b=c');
  });

  it('should return the error code when the fragment carries one', () => {
    const fragment = parseAuthFragment('#error=AUTHENTICATION_STATE_EXPIRED');
    expect(fragment.errorCode).toBe('AUTHENTICATION_STATE_EXPIRED');
    expect(fragment.token).toBeNull();
  });

  it('should return nulls when the fragment is empty', () => {
    expect(parseAuthFragment('')).toEqual({ token: null, errorCode: null });
    expect(parseAuthFragment('#')).toEqual({ token: null, errorCode: null });
  });

  it('should ignore unrelated fragment parameters', () => {
    expect(parseAuthFragment('#state=x&token=t').token).toBe('t');
  });
});

describe('clearAuthFragment', () => {
  it('should replace the URL without the fragment', () => {
    const replaceState = vi.fn();
    vi.stubGlobal('window', { history: { replaceState }, location: { pathname: '/auth/callback', search: '' } });

    clearAuthFragment();

    expect(replaceState).toHaveBeenCalledTimes(1);
    expect(replaceState).toHaveBeenCalledWith(null, '', '/auth/callback');
  });

  it('should preserve the query string', () => {
    const replaceState = vi.fn();
    vi.stubGlobal('window', { history: { replaceState }, location: { pathname: '/auth/callback', search: '?next=1' } });

    clearAuthFragment();

    expect(replaceState).toHaveBeenCalledWith(null, '', '/auth/callback?next=1');
  });
});
