import { CalendarCheck } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useMyBookings } from "@/api/queries";
import { useBookingActions } from "@/components/gym/SessionCard";
import { Badge, BookingBadge } from "@/components/ui/Badge";
import { Button, buttonClass } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Card";
import { EmptyState, ErrorState } from "@/components/ui/EmptyState";
import { PageHeader, Segmented } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { formatShortDate, formatTime } from "@/lib/format";
import { useGymContext } from "@/providers/GymProvider";

export default function MyBookingsPage() {
  const { t, i18n } = useTranslation();
  const { gym, slug } = useGymContext();
  const [scope, setScope] = useState<"Upcoming" | "Past">("Upcoming");
  const { data: bookings, isLoading, error, refetch } = useMyBookings(slug, scope);
  const { cancel } = useBookingActions();

  return (
    <div>
      <PageHeader
        title={t("bookings.title")}
        subtitle={t("bookings.subtitle")}
        actions={
          <Segmented
            label={t("bookings.title")}
            value={scope}
            onChange={setScope}
            options={[
              { value: "Upcoming", label: t("bookings.upcoming") },
              { value: "Past", label: t("bookings.past") },
            ]}
          />
        }
      />

      <div className="mt-8">
        {isLoading ? (
          <div className="space-y-3">
            <Skeleton className="h-20" />
            <Skeleton className="h-20" />
          </div>
        ) : error ? (
          <ErrorState message={errorMessage(error)} onRetry={() => refetch()} retryLabel={t("common.retry")} />
        ) : !bookings?.length ? (
          <EmptyState
            icon={CalendarCheck}
            text={scope === "Upcoming" ? t("bookings.emptyUpcoming") : t("bookings.emptyPast")}
            action={
              scope === "Upcoming" && (
                <Link to={`/g/${slug}/schedule`} className={buttonClass("primary", "sm")}>
                  {t("bookings.browse")}
                </Link>
              )
            }
          />
        ) : (
          <ul className="space-y-3">
            {bookings.map((b) => (
              <li key={b.id} className="flex flex-col gap-3 rounded-2xl border border-line bg-surface p-4 sm:flex-row sm:items-center sm:p-5">
                <div className="sm:w-44">
                  <p className="font-semibold">{formatShortDate(b.startsAt, i18n.language, gym.timeZone)}</p>
                  <p className="text-sm text-muted tabular-nums">
                    {formatTime(b.startsAt, i18n.language, gym.timeZone)} – {formatTime(b.endsAt, i18n.language, gym.timeZone)}
                  </p>
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-lg font-semibold">{b.className}</p>
                  {b.trainerName && <p className="text-sm text-muted">{b.trainerName}</p>}
                </div>
                <div className="flex items-center gap-3">
                  {b.isSessionCancelled ? (
                    <Badge tone="danger">{t("bookings.sessionCancelled")}</Badge>
                  ) : (
                    <BookingBadge status={b.status} position={b.waitlistPosition} />
                  )}
                  {scope === "Upcoming" && (
                    <Button
                      variant="ghost"
                      size="sm"
                      loading={cancel.isPending && cancel.variables === b.sessionId}
                      onClick={() => cancel.mutate(b.sessionId)}
                    >
                      {t("schedule.cancel")}
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
