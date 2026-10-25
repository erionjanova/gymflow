import { CalendarX, ChevronLeft, ChevronRight } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSchedule } from "@/api/queries";
import { SessionCard } from "@/components/gym/SessionCard";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Card";
import { EmptyState, ErrorState } from "@/components/ui/EmptyState";
import { PageHeader } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { cn } from "@/lib/utils";
import { addDays, formatCalendarDate, formatDayHeading, localDateOf, todayIn, zonedRangeOfDays } from "@/lib/format";
import type { Session } from "@/lib/types";
import { useGymContext } from "@/providers/GymProvider";

export function groupByDay(sessions: Session[], timeZone: string) {
  const groups = new Map<string, Session[]>();
  for (const s of sessions) {
    const day = localDateOf(s.startsAt, timeZone);
    groups.set(day, [...(groups.get(day) ?? []), s]);
  }
  return [...groups.entries()];
}

export default function SchedulePage() {
  const { t, i18n } = useTranslation();
  const { gym, slug } = useGymContext();
  const today = todayIn(gym.timeZone);
  const [weekStart, setWeekStart] = useState(today);

  const range = useMemo(() => {
    const from = weekStart === today ? new Date().toISOString() : zonedRangeOfDays(weekStart, 7, gym.timeZone).from;
    return { from, to: zonedRangeOfDays(weekStart, 7, gym.timeZone).to };
  }, [weekStart, today, gym.timeZone]);

  const { data: sessions, isLoading, error, refetch } = useSchedule(slug, range.from, range.to);
  const days = groupByDay(sessions ?? [], gym.timeZone);

  const weekLabel = `${formatCalendarDate(weekStart, i18n.language, { day: "numeric", month: "short" })} – ${formatCalendarDate(addDays(weekStart, 6), i18n.language, { day: "numeric", month: "short" })}`;

  return (
    <div>
      <PageHeader
        title={t("schedule.title")}
        subtitle={t("schedule.subtitle")}
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="icon"
              aria-label={t("schedule.prevWeek")}
              disabled={weekStart <= today}
              onClick={() => setWeekStart(addDays(weekStart, -7))}
            >
              <ChevronLeft className="size-4" />
            </Button>
            <span className="min-w-36 text-center text-sm font-semibold">{weekLabel}</span>
            <Button variant="outline" size="icon" aria-label={t("schedule.nextWeek")} onClick={() => setWeekStart(addDays(weekStart, 7))}>
              <ChevronRight className="size-4" />
            </Button>
          </div>
        }
      />

      <div className="mt-8 space-y-8">
        {isLoading ? (
          <div className="space-y-3">
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
            <Skeleton className="h-24" />
          </div>
        ) : error ? (
          <ErrorState message={errorMessage(error)} onRetry={() => refetch()} retryLabel={t("common.retry")} />
        ) : days.length === 0 ? (
          <EmptyState icon={CalendarX} text={t("schedule.empty")} />
        ) : (
          days.map(([day, list]) => (
            <section key={day} className="lg:grid lg:grid-cols-[13rem_1fr] lg:gap-8">
              <h2 className={cn("mb-3 text-sm font-semibold tracking-wide uppercase lg:sticky lg:top-24 lg:mb-0 lg:self-start lg:pt-6", day === today ? "text-accent" : "text-muted")}>
                {day === today ? t("schedule.today") : formatDayHeading(list[0]!.startsAt, i18n.language, gym.timeZone)}
              </h2>
              <div className="space-y-3">
                {list.map((s) => (
                  <SessionCard key={s.id} session={s} />
                ))}
              </div>
            </section>
          ))
        )}
      </div>
    </div>
  );
}
