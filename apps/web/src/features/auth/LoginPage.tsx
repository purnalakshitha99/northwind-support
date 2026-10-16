import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Field } from '../../components/Field';
import { useAuth } from './useAuth';

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string>();
  const [errors, setErrors] = useState<Record<string, string>>({});

  const rawReturnUrl = searchParams.get('returnUrl') ?? '/tickets';
  const returnUrl = rawReturnUrl.startsWith('/') && !rawReturnUrl.startsWith('//')
    ? rawReturnUrl
    : '/tickets';

  if (isAuthenticated && !location.pathname.startsWith('/login')) {
    return <Navigate to={returnUrl} replace />;
  }

  const validate = (): Record<string, string> => {
    const errs: Record<string, string> = {};

    if (!email.trim()) {
      errs.email = 'Email address is required.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      errs.email = 'Please enter a valid email address.';
    }

    if (!password) {
      errs.password = 'Password is required.';
    }

    return errs;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setSubmitError(undefined);

    const validationErrors = validate();
    setErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);
    try {
      await login({ email: email.trim(), password });
      navigate(returnUrl, { replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        setSubmitError(err.message || 'Invalid email or password.');
      } else if (err instanceof Error) {
        setSubmitError(err.message);
      } else {
        setSubmitError('Failed to sign in. Please check your connection.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div style={{ maxWidth: '420px', margin: '48px auto' }}>
      <div className="card">
        <header className="page-header" style={{ marginBottom: '16px' }}>
          <div>
            <h1>Agent Sign In</h1>
            <p className="card__note" style={{ margin: '4px 0 0', color: 'var(--colour-muted)' }}>
              Sign in with your support desk credentials.
            </p>
          </div>
        </header>

        {submitError && (
          <ErrorMessage message={submitError} />
        )}

        <form className="form" onSubmit={handleSubmit} noValidate>
          <Field id="login-email" label="Email address" error={errors.email}>
            {(props) => (
              <input
                {...props}
                type="email"
                autoComplete="email"
                autoFocus
                disabled={isSubmitting}
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value);
                  if (errors.email) {
                    setErrors((prev) => ({ ...prev, email: '' }));
                  }
                }}
                placeholder="e.g. alex.turner@northwind-support.example"
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={errors.password}>
            {(props) => (
              <input
                {...props}
                type="password"
                autoComplete="current-password"
                disabled={isSubmitting}
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value);
                  if (errors.password) {
                    setErrors((prev) => ({ ...prev, password: '' }));
                  }
                }}
              />
            )}
          </Field>

          <div style={{ marginTop: '16px' }}>
            <button
              type="submit"
              className="button button--primary"
              disabled={isSubmitting}
              style={{ width: '100%', justifyContent: 'center' }}
            >
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </button>
          </div>
        </form>

        <div
          className="notice"
          style={{
            marginTop: '24px',
            padding: '12px',
            fontSize: '0.85rem',
            background: 'var(--colour-page)',
            borderRadius: 'var(--radius)',
          }}
        >
          <strong>Development fixture:</strong>
          <div style={{ marginTop: '4px' }}>
            Email: <code>alex.turner@northwind-support.example</code>
          </div>
          <div>
            Password: <code>Password123!</code>
          </div>
        </div>
      </div>
    </div>
  );
}
