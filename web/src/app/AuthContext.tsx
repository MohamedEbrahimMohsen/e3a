import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { getCurrentUser, type CurrentUser } from '../lib/authApi';
import { ApiError, setUnauthorizedHandler } from '../lib/http';
import { clearToken, readToken, writeToken } from '../lib/tokenStorage';

export type AuthStatus = 'loading' | 'signedIn' | 'signedOut';

interface AuthContextValue {
  status: AuthStatus;
  signedIn: boolean;
  login: string;
  user: CurrentUser | null;
  completeSignIn: (token: string) => Promise<void>;
  signOut: () => void;
}

const AuthContext = createContext<AuthContextValue>({
  status: 'loading',
  signedIn: false,
  login: '',
  user: null,
  completeSignIn: async () => undefined,
  signOut: () => undefined,
});

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [status, setStatus] = useState<AuthStatus>('loading');

  const loadSession = useCallback(async () => {
    if (!readToken()) {
      setUser(null);
      setStatus('signedOut');
      return;
    }
    try {
      setUser(await getCurrentUser());
      setStatus('signedIn');
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        clearToken();
      }
      setUser(null);
      setStatus('signedOut');
    }
  }, []);

  const signOut = useCallback(() => {
    clearToken();
    setUser(null);
    setStatus('signedOut');
  }, []);

  const completeSignIn = useCallback(async (token: string) => {
    writeToken(token);
    await loadSession();
  }, [loadSession]);

  useEffect(() => { void loadSession(); }, [loadSession]);

  useEffect(() => {
    setUnauthorizedHandler(signOut);
    return () => setUnauthorizedHandler(null);
  }, [signOut]);

  return (
    <AuthContext.Provider value={{ status, signedIn: status === 'signedIn', login: user?.gitHubLogin ?? '', user, completeSignIn, signOut }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  return useContext(AuthContext);
}
