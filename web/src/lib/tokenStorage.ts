const TOKEN_STORAGE_KEY = 'e3a.token';

export function readToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY);
}

export function writeToken(token: string): void {
  localStorage.setItem(TOKEN_STORAGE_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
}
