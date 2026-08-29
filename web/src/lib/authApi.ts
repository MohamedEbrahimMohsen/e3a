import { config } from './config';
import { requestJson } from './http';

export interface CurrentUser {
  id: string;
  gitHubId: number | null;
  gitHubLogin: string | null;
  displayName: string | null;
  avatarUrl: string | null;
  createdAt: string;
}

export function gitHubLoginUrl(): string {
  return `${config.apiBaseUrl}/auth/github/login`;
}

export function getCurrentUser(): Promise<CurrentUser> {
  return requestJson<CurrentUser>('/auth/me');
}
