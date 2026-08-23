import { createContext, useContext, useState, type ReactNode } from 'react';

interface AuthContextValue {
  signedIn: boolean;
  login: string;
  signIn: () => void;
}

const MOCK_LOGIN = 'mohamed-dive';

const AuthContext = createContext<AuthContextValue>({ signedIn: false, login: MOCK_LOGIN, signIn: () => undefined });

export function AuthProvider({ children }: { children: ReactNode }) {
  const [signedIn, setSignedIn] = useState(false);

  return (
    <AuthContext.Provider value={{ signedIn, login: MOCK_LOGIN, signIn: () => setSignedIn(true) }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  return useContext(AuthContext);
}
