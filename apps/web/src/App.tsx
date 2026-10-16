import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { AuthProvider } from './features/auth/AuthContext';
import { useAuth } from './features/auth/useAuth';

function AppShell() {
  const { user, isAuthenticated, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        {isAuthenticated && (
          <nav className="app__nav" aria-label="Main">
            <NavLink to="/tickets">Tickets</NavLink>
            <NavLink to="/customers">Customers</NavLink>
          </nav>
        )}

        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '16px' }}>
          {isAuthenticated && user ? (
            <>
              <span style={{ fontSize: '0.85rem', color: 'var(--colour-muted)' }}>
                Signed in as <strong style={{ color: 'var(--colour-text)' }}>{user.fullName}</strong>
              </span>
              <button
                type="button"
                className="button"
                style={{ padding: '4px 10px', fontSize: '0.85rem' }}
                onClick={handleLogout}
              >
                Sign out
              </button>
            </>
          ) : (
            <Link
              to="/login"
              className="button"
              style={{ padding: '4px 10px', fontSize: '0.85rem' }}
            >
              Sign in
            </Link>
          )}
        </div>
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}

/** Application shell with AuthProvider context wrapping all routed views. */
export function App() {
  return (
    <AuthProvider>
      <AppShell />
    </AuthProvider>
  );
}
