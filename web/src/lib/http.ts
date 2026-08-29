import { config } from './config';
import { clearToken, readToken } from './tokenStorage';

const NO_CONTENT_STATUS = 204;
const UNAUTHORIZED_STATUS = 401;

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;

  constructor(status: number, code: string | null, message: string) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

export interface RequestOptions {
  method?: string;
  body?: unknown;
  formData?: FormData;
  signal?: AbortSignal;
}

let unauthorizedHandler: (() => void) | null = null;

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

async function readErrorBody(response: Response): Promise<{ code?: string; message?: string }> {
  try {
    return await response.json();
  } catch {
    return {};
  }
}

export async function requestJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = {};

  const token = readToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  let body: BodyInit | undefined;
  if (options.formData) {
    body = options.formData;
  } else if (options.body !== undefined) {
    body = JSON.stringify(options.body);
    headers['Content-Type'] = 'application/json';
  }

  const response = await fetch(`${config.apiBaseUrl}${path}`, {
    method: options.method ?? 'GET',
    headers,
    body,
    signal: options.signal,
  });

  if (response.status === UNAUTHORIZED_STATUS) {
    clearToken();
    unauthorizedHandler?.();
  }

  if (!response.ok) {
    const errorBody = await readErrorBody(response);
    throw new ApiError(response.status, errorBody.code ?? null, errorBody.message ?? `API request failed with status ${response.status}`);
  }

  if (response.status === NO_CONTENT_STATUS) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
