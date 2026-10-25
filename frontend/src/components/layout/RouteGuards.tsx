import { Loader2 } from "lucide-react";
import { Navigate, Outlet, useLocation } from "react-router";
import { useAuth } from "@/providers/AuthProvider";
import { useGymContext } from "@/providers/GymProvider";

export const FullPageSpinner = () => (
  <div className="grid min-h-[50vh] place-items-center">
    <Loader2 className="size-6 animate-spin text-accent" />
  </div>
);

export function RequireAuth() {
  const { user, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) return <FullPageSpinner />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  return <Outlet />;
}

export function RequirePlatformAdmin() {
  const { user, isPlatformAdmin, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) return <FullPageSpinner />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (!isPlatformAdmin) return <Navigate to="/gyms" replace />;
  return <Outlet />;
}

export function GuestOnly() {
  const { user, isLoading } = useAuth();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;

  if (isLoading) return <FullPageSpinner />;
  if (user) return <Navigate to={from ?? "/gyms"} replace />;
  return <Outlet />;
}

export function RequireGymMember() {
  const { user, isLoading } = useAuth();
  const { isMember, slug } = useGymContext();
  const location = useLocation();

  if (isLoading) return <FullPageSpinner />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  if (!isMember) return <Navigate to={`/g/${slug}`} replace />;
  return <Outlet />;
}

export function RequireGymStaff() {
  const { isStaff, slug } = useGymContext();
  if (!isStaff) return <Navigate to={`/g/${slug}`} replace />;
  return <Outlet />;
}

export function RequireGymOwner() {
  const { isOwner, slug } = useGymContext();
  if (!isOwner) return <Navigate to={`/g/${slug}/admin`} replace />;
  return <Outlet />;
}
