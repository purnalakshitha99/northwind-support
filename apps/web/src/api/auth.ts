import { request } from './client';
import type { CurrentUser, LoginRequest, LoginResponse } from '../types/api';

/** Calls POST /api/auth/login with agent email and password. */
export function login(credentials: LoginRequest): Promise<LoginResponse> {
  return request<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  });
}

/** Calls GET /api/auth/me to fetch current agent profile. */
export function getCurrentUser(): Promise<CurrentUser> {
  return request<CurrentUser>('/auth/me');
}
