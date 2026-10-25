import { useQueryClient } from "@tanstack/react-query";
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import i18n from "@/i18n";
import { api, setUnauthorizedHandler, tokenStore } from "@/lib/api";
import type { AuthResponse, Language, User } from "@/lib/types";

export interface RegisterInput {
  fullName: string;
  email: string;
  password: string;
  preferredLanguage: Language;
}

interface AuthContextValue {
  user: User | null;
  isLoading: boolean;
  isPlatformAdmin: boolean;
  login: (email: string, password: string) => Promise<User>;
  register: (input: RegisterInput) => Promise<User>;
  startSession: (response: AuthResponse) => User;
  logout: () => void;
  setUser: (user: User) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(() => tokenStore.get() !== null);

  const logout = useCallback(() => {
    tokenStore.set(null);
    setUser(null);
    queryClient.clear();
  }, [queryClient]);

  useEffect(() => {
    if (!tokenStore.get()) return;
    api
      .get<User>("/api/auth/me")
      .then(setUser)
      .catch(() => tokenStore.set(null))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(logout);
    return () => setUnauthorizedHandler(null);
  }, [logout]);

  const startSession = useCallback(
    (response: AuthResponse) => {
      tokenStore.set(response.token);
      queryClient.clear();
      setUser(response.user);
      return response.user;
    },
    [queryClient],
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const user = startSession(await api.post<AuthResponse>("/api/auth/login", { email, password }));
      if (user.preferredLanguage !== i18n.resolvedLanguage) await i18n.changeLanguage(user.preferredLanguage);
      return user;
    },
    [startSession],
  );

  const register = useCallback(
    async (input: RegisterInput) => startSession(await api.post<AuthResponse>("/api/auth/register", input)),
    [startSession],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      isPlatformAdmin: user?.isPlatformAdmin ?? false,
      login,
      register,
      startSession,
      logout,
      setUser,
    }),
    [user, isLoading, login, register, startSession, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
