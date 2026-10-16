import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as authApi from '../../api/auth';
import { clearAuthSession, setAuthSession } from '../../api/client';
import { AuthProvider } from './AuthContext';
import { ProtectedRoute } from './ProtectedRoute';

vi.mock('../../api/auth', () => ({
  getCurrentUser: vi.fn(),
  login: vi.fn(),
}));

describe('ProtectedRoute', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    clearAuthSession();
  });

  it('redirects unauthenticated user to /login with returnUrl', () => {
    render(
      <MemoryRouter initialEntries={['/tickets/12']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Mock</div>} />
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets/:id" element={<div>Protected Ticket Detail</div>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(screen.getByText('Login Page Mock')).toBeInTheDocument();
    expect(screen.queryByText('Protected Ticket Detail')).not.toBeInTheDocument();
  });

  it('renders protected child component when user is authenticated', async () => {
    const agent = {
      id: 1,
      fullName: 'Alex Turner',
      email: 'alex.turner@northwind-support.example',
    };
    vi.mocked(authApi.getCurrentUser).mockResolvedValueOnce(agent);
    setAuthSession('valid-token', agent);

    render(
      <MemoryRouter initialEntries={['/tickets']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<div>Login Page Mock</div>} />
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets" element={<div>Protected Ticket List</div>} />
            </Route>
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByText('Protected Ticket List')).toBeInTheDocument();
    expect(screen.queryByText('Login Page Mock')).not.toBeInTheDocument();
  });
});
