import { useMutation, useQueryClient } from "@tanstack/react-query";
import { CalendarDays, CreditCard, Home, LayoutDashboard, ListChecks, MapPin, Phone, ShieldAlert } from "lucide-react";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link, NavLink, Outlet, useLocation, useParams } from "react-router";
import { toast } from "sonner";
import { keys, useGym } from "@/api/queries";
import { FullPageSpinner } from "@/components/layout/RouteGuards";
import { RoleBadge } from "@/components/ui/Badge";
import { Button, buttonClass } from "@/components/ui/Button";
import { errorMessage } from "@/i18n";
import { api, ApiError, gymPath } from "@/lib/api";
import type { GymMembership } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useAuth } from "@/providers/AuthProvider";
import { GymProvider } from "@/providers/GymProvider";
import NotFoundPage from "@/pages/NotFoundPage";

const tabClass = ({ isActive }: { isActive: boolean }) =>
  cn(
    "flex shrink-0 items-center gap-2 rounded-full px-4 py-2 text-sm font-medium transition-colors",
    isActive ? "bg-accent text-accent-fg" : "text-muted hover:bg-surface-2 hover:text-fg",
  );

export default function GymLayout() {
  const { t } = useTranslation();
  const { slug = "" } = useParams();
  const { user } = useAuth();
  const location = useLocation();
  const queryClient = useQueryClient();
  const { data, isPending, error } = useGym(slug);

  const join = useMutation({
    mutationFn: () => api.post<GymMembership>(gymPath(slug, "/join")),
    onSuccess: () => {
      toast.success(t("gym.joined", { name: data?.gym.name }));
      queryClient.invalidateQueries({ queryKey: keys.gym(slug) });
      queryClient.invalidateQueries({ queryKey: ["my-gyms"] });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  if (isPending) return <FullPageSpinner />;

  if (error instanceof ApiError && error.code === "gym_suspended") {
    return (
      <div className="mx-auto grid max-w-lg place-items-center gap-4 px-4 py-24 text-center">
        <ShieldAlert className="size-10 text-warning" />
        <h1 className="text-2xl font-semibold">{t("gym.suspendedTitle")}</h1>
        <p className="text-muted">{t("gym.suspendedText")}</p>
      </div>
    );
  }

  if (!data) return <NotFoundPage />;

  const { gym, role } = data;
  const isStaff = role === "Owner" || role === "Trainer";

  const tabs = [
    { to: `/g/${slug}`, label: t("gym.tabs.overview"), icon: Home, end: true },
    { to: `/g/${slug}/schedule`, label: t("gym.tabs.schedule"), icon: CalendarDays, end: true },
    ...(role ? [{ to: `/g/${slug}/bookings`, label: t("gym.tabs.bookings"), icon: ListChecks, end: true }] : []),
    ...(role ? [{ to: `/g/${slug}/membership`, label: t("gym.tabs.membership"), icon: CreditCard, end: true }] : []),
    ...(isStaff ? [{ to: `/g/${slug}/admin`, label: t("gym.tabs.admin"), icon: LayoutDashboard, end: false }] : []),
  ];

  return (
    <GymProvider gym={gym} role={role}>
      <div className="gym-theme" style={{ "--brand": gym.brandColor } as CSSProperties}>
        <section className="relative overflow-hidden border-b border-line">
          <div className="hero-glow pointer-events-none absolute inset-0" />
          <div className="relative mx-auto max-w-[100rem] px-4 pt-8 pb-4 sm:px-6 lg:px-10 2xl:px-16">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-4">
                <span className="grid size-14 shrink-0 place-items-center rounded-2xl bg-accent font-display text-2xl font-bold text-accent-fg shadow-lg shadow-accent/25">
                  {gym.name.charAt(0)}
                </span>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h1 className="truncate text-2xl font-semibold sm:text-3xl">{gym.name}</h1>
                    {role && <RoleBadge role={role} />}
                  </div>
                  <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted">
                    {gym.address && (
                      <span className="flex items-center gap-1.5">
                        <MapPin className="size-3.5" />
                        {gym.address}
                      </span>
                    )}
                    {gym.phone && (
                      <a href={`tel:${gym.phone}`} className="flex items-center gap-1.5 hover:text-fg">
                        <Phone className="size-3.5" />
                        {gym.phone}
                      </a>
                    )}
                  </div>
                </div>
              </div>

              {!role &&
                (user ? (
                  <Button onClick={() => join.mutate()} loading={join.isPending}>
                    {t("gym.join")}
                  </Button>
                ) : (
                  <Link to="/register" state={{ from: location.pathname }} className={buttonClass("primary")}>
                    {t("gym.signUpToJoin")}
                  </Link>
                ))}
            </div>

            <nav className="scrollbar-thin -mx-1 mt-6 flex gap-1 overflow-x-auto px-1 pb-1" aria-label={t("gym.tabs.label")}>
              {tabs.map((tab) => (
                <NavLink key={tab.to} to={tab.to} end={tab.end} className={tabClass}>
                  <tab.icon className="size-4" />
                  {tab.label}
                </NavLink>
              ))}
            </nav>
          </div>
        </section>

        <div className="mx-auto max-w-[100rem] px-4 py-8 sm:px-6 lg:px-10 2xl:px-16">
          <Outlet />
        </div>
      </div>
    </GymProvider>
  );
}
