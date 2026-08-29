export interface AuthFragment {
  token: string | null;
  errorCode: string | null;
}

export function parseAuthFragment(hash: string): AuthFragment {
  const parameters = new URLSearchParams(hash.startsWith('#') ? hash.slice(1) : hash);
  return { token: parameters.get('token'), errorCode: parameters.get('error') };
}

export function clearAuthFragment(): void {
  window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}`);
}
