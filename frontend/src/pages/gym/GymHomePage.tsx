import { ArrowRight, CalendarX } from "lucide-react";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useClasses, usePlans, useSchedule } from "@/api/queries";
import { PlanCard } from "@/components/gym/PlanCard";
import { SessionCard } from "@/components/gym/SessionCard";
import { buttonClass } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { useGymContext } from "@/providers/GymProvider";

export default function GymHomePage() {
  const { t } = useTranslation();
  const { gym, slug, isMember } = useGymContext();
  const range = useMemo(() => {
    const now = new Date();
    return { from: now.toISOString(), to: new Date(now.getTime() + 3 * 86_400_000).toISOString() };
  }, []);

  const { data: sessions, isLoading } = useSchedule(slug, range.from, range.to);
  const { data: plans } = usePlans(slug);
  const { data: classes } = useClasses(slug);

  return (
    <div className="grid gap-10 lg:grid-cols-[1fr_22rem]">
      <div className="space-y-10">
        {gym.description && <p className="max-w-2xl text-lg leading-relaxed text-muted">{gym.description}</p>}

        <section>
          <div className="mb-4 flex items-end justify-between gap-4">
            <h2 className="text-2xl font-semibold">{t("gymHome.upcoming")}</h2>
            <Link to={`/g/${slug}/schedule`} className="flex min-h-6 items-center gap-1 text-sm font-semibold text-accent hover:underline">
              {t("gymHome.fullSchedule")}
              <ArrowRight className="size-4" />
            </Link>
          </div>
          {isLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-24" />
              <Skeleton className="h-24" />
            </div>
          ) : !sessions?.length ? (
            <EmptyState icon={CalendarX} text={t("schedule.empty")} />
          ) : (
            <div className="space-y-3">
              {sessions.slice(0, 4).map((s) => (
                <SessionCard key={s.id} session={s} />
              ))}
            </div>
          )}
        </section>

        {!!classes?.length && (
          <section>
            <h2 className="mb-4 text-2xl font-semibold">{t("gymHome.classes")}</h2>
            <div className="grid gap-3 sm:grid-cols-2 2xl:grid-cols-3">
              {classes.map((c) => (
                <div key={c.id} className="rounded-2xl border border-line bg-surface p-5">
                  <p className="font-semibold">{c.name}</p>
                  {c.description && <p className="mt-1 text-sm text-muted">{c.description}</p>}
                  <p className="mt-3 text-xs font-medium text-muted">
                    {t("common.minutes", { count: c.durationMinutes })} · {t("gymHome.capacity", { count: c.defaultCapacity })}
                  </p>
                </div>
              ))}
            </div>
          </section>
        )}
      </div>

      <aside className="space-y-4">
        <h2 className="text-2xl font-semibold">{t("gymHome.memberships")}</h2>
        {plans?.map((plan) => (
          <PlanCard
            key={plan.id}
            plan={plan}
            action={
              <Link to={isMember ? `/g/${slug}/membership` : "/register"} className={buttonClass("outline", "sm", "w-full")}>
                {t("gymHome.choosePlan")}
              </Link>
            }
          />
        ))}
      </aside>
    </div>
  );
}
