import { useQuery } from "@tanstack/react-query";
import { api, gymPath } from "@/lib/api";
import type {
  BillingOverview,
  Booking,
  GymClass,
  GymMembership,
  Insights,
  Invitation,
  InvitationPreview,
  Member,
  MyGym,
  MySubscription,
  Plan,
  PlatformGym,
  PlatformStats,
  RosterEntry,
  Session,
} from "@/lib/types";
import { useAuth } from "@/providers/AuthProvider";

export const keys = {
  myGyms: (userId?: string) => ["my-gyms", userId] as const,
  gym: (slug: string) => ["gym", slug] as const,
  plans: (slug: string) => ["gym", slug, "plans"] as const,
  classes: (slug: string) => ["gym", slug, "classes"] as const,
  schedule: (slug: string) => ["gym", slug, "schedule"] as const,
  sessions: (slug: string) => ["gym", slug, "sessions"] as const,
  roster: (slug: string, sessionId: string) => ["gym", slug, "roster", sessionId] as const,
  bookings: (slug: string) => ["gym", slug, "bookings"] as const,
  members: (slug: string) => ["gym", slug, "members"] as const,
  invitations: (slug: string) => ["gym", slug, "invitations"] as const,
  subscription: (slug: string) => ["gym", slug, "subscription"] as const,
  billing: (slug: string) => ["gym", slug, "billing"] as const,
  insights: (slug: string) => ["gym", slug, "insights"] as const,
  platform: ["platform"] as const,
};

export function useMyGyms() {
  const { user } = useAuth();
  return useQuery({
    queryKey: keys.myGyms(user?.id),
    queryFn: () => api.get<MyGym[]>("/api/auth/me/gyms"),
    enabled: !!user,
  });
}

export function useGym(slug: string) {
  const { user, isLoading } = useAuth();
  return useQuery({
    queryKey: [...keys.gym(slug), user?.id],
    queryFn: () => api.get<GymMembership>(gymPath(slug)),
    enabled: !isLoading,
  });
}

export const usePlans = (slug: string, admin = false) =>
  useQuery({
    queryKey: [...keys.plans(slug), admin],
    queryFn: () => api.get<Plan[]>(gymPath(slug, admin ? "/admin/plans" : "/plans")),
  });

export const useClasses = (slug: string, admin = false) =>
  useQuery({
    queryKey: [...keys.classes(slug), admin],
    queryFn: () => api.get<GymClass[]>(gymPath(slug, admin ? "/admin/classes" : "/classes")),
  });

export function useSchedule(slug: string, from: string, to: string) {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.schedule(slug), from, to, user?.id],
    queryFn: () =>
      api.get<Session[]>(gymPath(slug, `/schedule?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`)),
  });
}

export const useStaffSessions = (slug: string, from: string, to: string) =>
  useQuery({
    queryKey: [...keys.sessions(slug), from, to],
    queryFn: () =>
      api.get<Session[]>(gymPath(slug, `/admin/sessions?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`)),
  });

export const useRoster = (slug: string, sessionId: string | null) =>
  useQuery({
    queryKey: keys.roster(slug, sessionId ?? ""),
    queryFn: () => api.get<RosterEntry[]>(gymPath(slug, `/admin/sessions/${sessionId}/roster`)),
    enabled: !!sessionId,
  });

export const useMyBookings = (slug: string, scope: "Upcoming" | "Past") =>
  useQuery({
    queryKey: [...keys.bookings(slug), scope],
    queryFn: () => api.get<Booking[]>(gymPath(slug, `/me/bookings?scope=${scope}`)),
  });

export const useMembers = (slug: string) =>
  useQuery({
    queryKey: keys.members(slug),
    queryFn: () => api.get<Member[]>(gymPath(slug, "/admin/members")),
  });

export const useInvitations = (slug: string) =>
  useQuery({
    queryKey: keys.invitations(slug),
    queryFn: () => api.get<Invitation[]>(gymPath(slug, "/admin/invitations")),
  });

export const useInvitationPreview = (token: string) =>
  useQuery({
    queryKey: ["invitation", token],
    queryFn: () => api.get<InvitationPreview>(`/api/invitations/${encodeURIComponent(token)}`),
  });

export const useMySubscription = (slug: string, enabled = true) =>
  useQuery({
    queryKey: keys.subscription(slug),
    queryFn: async () => (await api.get<MySubscription | undefined>(gymPath(slug, "/me/subscription"))) ?? null,
    enabled,
  });

export const useBillingOverview = (slug: string) =>
  useQuery({
    queryKey: keys.billing(slug),
    queryFn: () => api.get<BillingOverview>(gymPath(slug, "/admin/billing")),
  });

export const useInsights = (slug: string) =>
  useQuery({
    queryKey: keys.insights(slug),
    queryFn: () => api.get<Insights>(gymPath(slug, "/admin/insights")),
    placeholderData: (previous) => previous,
  });

export const usePlatformStats = () =>
  useQuery({ queryKey: [...keys.platform, "stats"], queryFn: () => api.get<PlatformStats>("/api/platform/stats") });

export const usePlatformGyms = () =>
  useQuery({ queryKey: [...keys.platform, "gyms"], queryFn: () => api.get<PlatformGym[]>("/api/platform/gyms") });
