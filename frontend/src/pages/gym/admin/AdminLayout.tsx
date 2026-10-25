import { CalendarRange, Dumbbell, LayoutDashboard, Settings, Tags, Users } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Navigate, NavLink, Outlet, useLocation } from "react-router";
import { cn } from "@/lib/utils";
import { useGymContext } from "@/providers/GymProvider";

export default function AdminLayout() {
  const { t } = useTranslation();
  const { slug, isOwner } = useGymContext();
  const { pathname } = useLocation();
  const base = `/g/${slug}/admin`;

  if (!isOwner && (pathname === base || pathname === `${base}/`)) return <Navigate to={`${base}/sessions`} replace />;

  const links = [
    ...(isOwner ? [{ to: base, label: t("admin.nav.dashboard"), icon: LayoutDashboard, end: true }] : []),
    { to: `${base}/sessions`, label: t("admin.nav.sessions"), icon: CalendarRange, end: false },
    ...(isOwner
      ? [
          { to: `${base}/classes`, label: t("admin.nav.classes"), icon: Dumbbell, end: false },
          { to: `${base}/plans`, label: t("admin.nav.plans"), icon: Tags, end: false },
        ]
      : []),
    { to: `${base}/members`, label: t("admin.nav.members"), icon: Users, end: false },
    ...(isOwner ? [{ to: `${base}/settings`, label: t("admin.nav.settings"), icon: Settings, end: false }] : []),
  ];

  return (
    <div className="grid gap-8 lg:grid-cols-[13rem_1fr]">
      <nav aria-label={t("gym.tabs.admin")} className="scrollbar-thin -mx-1 flex gap-1 overflow-x-auto px-1 lg:mx-0 lg:flex-col lg:overflow-visible lg:px-0">
        {links.map((l) => (
          <NavLink
            key={l.to}
            to={l.to}
            end={l.end}
            className={({ isActive }) =>
              cn(
                "flex shrink-0 items-center gap-3 rounded-xl px-4 py-2.5 text-sm font-medium transition-colors",
                isActive ? "bg-surface text-fg shadow-sm ring-1 ring-line" : "text-muted hover:bg-surface-2 hover:text-fg",
              )
            }
          >
            <l.icon className="size-4" />
            {l.label}
          </NavLink>
        ))}
      </nav>
      <div className="min-w-0">
        <Outlet />
      </div>
    </div>
  );
}
