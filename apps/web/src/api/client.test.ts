import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  ApiError,
  clearAuthSession,
  getAuthToken,
  onUnauthorized,
  request,
  setAuthSession,
} from './client';

describe('api client', () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    localStorage.clear();
    clearAuthSession();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('attaches Authorization: Bearer <token> when a token is in session', async () => {
    setAuthSession('sample-jwt-token', { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' });

    let capturedHeaders: Record<string, string> | undefined;
    globalThis.fetch = vi.fn().mockImplementation((_url, init) => {
      capturedHeaders = init?.headers as Record<string, string>;
      return Promise.resolve(new Response(JSON.stringify({ ok: true }), { status: 200 }));
    });

    await request<{ ok: boolean }>('/test-endpoint');

    expect(capturedHeaders?.Authorization).toBe('Bearer sample-jwt-token');
  });

  it('does not attach Authorization header when unauthenticated', async () => {
    let capturedHeaders: Record<string, string> | undefined;
    globalThis.fetch = vi.fn().mockImplementation((_url, init) => {
      capturedHeaders = init?.headers as Record<string, string>;
      return Promise.resolve(new Response(JSON.stringify({ ok: true }), { status: 200 }));
    });

    await request<{ ok: boolean }>('/test-endpoint');

    expect(capturedHeaders?.Authorization).toBeUndefined();
  });

  it('clears session and triggers onUnauthorized callback when response is 401', async () => {
    setAuthSession('expired-token', { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' });
    const unauthorizedSpy = vi.fn();
    const unsubscribe = onUnauthorized(unauthorizedSpy);

    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized', detail: 'Token has expired' }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await expect(request('/protected')).rejects.toThrow(ApiError);

    expect(getAuthToken()).toBeNull();
    expect(unauthorizedSpy).toHaveBeenCalledTimes(1);

    unsubscribe();
  });
});
