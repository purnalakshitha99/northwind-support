import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { getCurrentUser, login as apiLogin } from '../../api/auth';
import {
  clearAuthSession,
  getAuthToken,
  getStoredAgent,
  onUnauthorized,
  setAuthSession,
} from '../../api/client';
import type { CurrentUser, LoginRequest } from '../../types/api';
import { AuthContext, type AuthContextValue } from './context';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(() => getStoredAgent());
  const [isLoading, setIsLoading] = useState<boolean>(() => {
    // If a token is stored, verify it once against /api/auth/me on mount
    return Boolean(getAuthToken());
  });

  const logout = useCallback(() => {
    clearAuthSession();
    setUser(null);
  }, []);

  const login = useCallback(async (credentials: LoginRequest) => {
    const response = await apiLogin(credentials);
    setAuthSession(response.token, response.agent);
    setUser(response.agent);
  }, []);

  useEffect(() => {
    // Intercept 401 responses triggered anywhere in the app
    const unsubscribe = onUnauthorized(() => {
      setUser(null);
    });

    return unsubscribe;
  }, []);

  useEffect(() => {
    const token = getAuthToken();
    if (!token) {
      return;
    }

    let isMounted = true;
    getCurrentUser()
      .then((currentUser) => {
        if (isMounted) {
          setUser(currentUser);
        }
      })
      .catch(() => {
        if (isMounted) {
          clearAuthSession();
          setUser(null);
        }
      })
      .finally(() => {
        if (isMounted) {
          setIsLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: Boolean(user),
      isLoading,
      login,
      logout,
    }),
    [user, isLoading, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
