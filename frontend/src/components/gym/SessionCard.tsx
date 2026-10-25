import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Clock, User as UserIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate } from "react-router";
import { toast } from "sonner";
import { keys } from "@/api/queries";
import { BookingBadge } from "@/components/ui/Badge";
import { Button, buttonClass } from "@/components/ui/Button";
import { errorMessage } from "@/i18n";
import { api, ApiError, gymPath } from "@/lib/api";
import { formatTime } from "@/lib/format";
import type { Booking, Session } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useAuth } from "@/providers/AuthProvider";
import { useGymContext } from "@/providers/GymProvider";

export function useBookingActions() {
  const { t } = useTranslation();
  const { slug } = useGymContext();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: keys.schedule(slug) });
    queryClient.invalidateQueries({ queryKey: keys.bookings(slug) });
  };

  const book = useMutation({
    mutationFn: (sessionId: string) => api.post<Booking>(gymPath(slug, `/sessions/${sessionId}/booking`)),
    onSuccess: (b) => {
      if (b.status === "Waitlisted") toast(t("booking.waitlisted", { position: b.waitlistPosition }));
      else toast.success(t("booking.booked", { name: b.className }));
      refresh();
    },
    onError: (e) => {
      if (e instanceof ApiError && e.code === "membership_required") {
        toast.error(errorMessage(e), {
          action: { label: t("booking.getMembership"), onClick: () => navigate(`/g/${slug}/membership`) },
        });
      } else {
        toast.error(errorMessage(e));
      }
      refresh();
    },
  });

  const cancel = useMutation({
    mutationFn: (sessionId: string) => api.delete<Booking>(gymPath(slug, `/sessions/${sessionId}/booking`)),
    onSuccess: () => {
      toast(t("booking.cancelled"));
      refresh();
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  return { book, cancel };
}

export function SessionCard({ session }: { session: Session }) {
  const { t, i18n } = useTranslation();
  const { gym, isMember } = useGymContext();
  const { user } = useAuth();
  const location = useLocation();
  const { book, cancel } = useBookingActions();

  const started = new Date(session.startsAt).getTime() <= Date.now();
  const hasBooking = session.myStatus === "Booked" || session.myStatus === "Waitlisted";
  const full = session.spotsLeft === 0;
  const fill = Math.min(100, (session.bookedCount / session.capacity) * 100);
  const busy = (book.isPending && book.variables === session.id) || (cancel.isPending && cancel.variables === session.id);

  return (
    <div
      className={cn(
        "flex flex-col gap-4 rounded-2xl border border-line bg-surface p-4 sm:flex-row sm:items-center sm:p-5",
        hasBooking && "border-accent/40 ring-1 ring-accent/20",
      )}
    >
      <div className="flex items-center gap-4 sm:w-28 sm:flex-col sm:items-start sm:gap-0.5">
        <span className="font-display text-2xl font-semibold tabular-nums">{formatTime(session.startsAt, i18n.language, gym.timeZone)}</span>
        <span className="flex items-center gap-1 text-xs text-muted">
          <Clock className="size-3" />
          {t("common.minutes", { count: Math.round((new Date(session.endsAt).getTime() - new Date(session.startsAt).getTime()) / 60000) })}
        </span>
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="text-lg font-semibold">{session.className}</h3>
          {session.myStatus && <BookingBadge status={session.myStatus} position={session.myWaitlistPosition} />}
        </div>
        {session.trainerName && (
          <p className="mt-0.5 flex items-center gap-1.5 text-sm text-muted">
            <UserIcon className="size-3.5" />
            {session.trainerName}
          </p>
        )}
        <div className="mt-3 flex items-center gap-3">
          <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-surface-2">
            <div className={cn("h-full rounded-full transition-all", full ? "bg-warning" : "bg-accent")} style={{ width: `${fill}%` }} />
          </div>
          <span className="shrink-0 text-xs font-medium text-muted tabular-nums">
            {full
              ? session.waitlistCount > 0
                ? t("schedule.fullWithWaitlist", { count: session.waitlistCount })
                : t("schedule.full")
              : t("schedule.spotsLeft", { count: session.spotsLeft })}
          </span>
        </div>
      </div>

      <div className="sm:w-40 sm:text-right">
        {started ? (
          <span className="text-sm text-muted">{t("schedule.started")}</span>
        ) : !user ? (
          <Link to="/login" state={{ from: location.pathname }} className={buttonClass("outline", "sm", "w-full sm:w-auto")}>
            {t("schedule.loginToBook")}
          </Link>
        ) : !isMember ? (
          <span className="text-sm text-muted">{t("schedule.joinToBook")}</span>
        ) : hasBooking ? (
          <Button variant="outline" size="sm" className="w-full sm:w-auto" loading={busy} onClick={() => cancel.mutate(session.id)}>
            {session.myStatus === "Waitlisted" ? t("schedule.leaveWaitlist") : t("schedule.cancel")}
          </Button>
        ) : (
          <Button size="sm" className="w-full sm:w-auto" loading={busy} onClick={() => book.mutate(session.id)}>
            {full ? t("schedule.joinWaitlist") : t("schedule.book")}
          </Button>
        )}
      </div>
    </div>
  );
}
