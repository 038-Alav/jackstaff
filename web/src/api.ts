const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080/api';
const TOKEN_KEY = 'jackstaff.accessToken';

export function getToken(): string | null { return sessionStorage.getItem(TOKEN_KEY); }
export function setToken(token: string): void { sessionStorage.setItem(TOKEN_KEY, token); }
export function clearToken(): void { sessionStorage.removeItem(TOKEN_KEY); }

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  const token = getToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, { ...init, headers });
  } catch {
    throw new Error('No se pudo conectar con JACKSTAFF API. Revisa que el entorno esté levantado.');
  }

  if (!response.ok) {
    let message = `Error ${response.status}`;
    try {
      const body = await response.json() as { error?: string; detail?: string; title?: string };
      message = body.error ?? body.detail ?? body.title ?? message;
    } catch { /* The response did not include a JSON problem document. */ }
    throw new Error(message);
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

export const apiPost = <T>(path: string, body: unknown) =>
  api<T>(path, { method: 'POST', body: JSON.stringify(body) });
export const apiPatch = <T>(path: string, body: unknown) =>
  api<T>(path, { method: 'PATCH', body: JSON.stringify(body) });
