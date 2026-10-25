import { CreditCard } from "lucide-react";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useBillingOverview, useMembers, useStaffSessions } from "@/api/queries";
import { SubscriptionBadge } from "@/components/ui/Badge";
import { Card, Skeleton } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { PageHeader, StatCard } from "@/components/ui/PageHeader";
import { formatDate, formatPrice, formatShortDate, formatTime } from "@/lib/format";
import { useGymContext } from "@/providers/GymProvider";
import { InsightsCharts } from "./InsightsCharts";

export default function DashboardPage() {
  const { t, i18n } = useTranslation();
  const { gym, slug } = useGymContext();
  const range = useMemo(() => {
    const now = new Date();
    return { from: now.toISOString(), to: new Date(now.getTime() + 7 * 86_400_000).toISOString() };
  }, []);

  const { data: billing, isLoading } = useBillingOverview(slug);
  const { data: members } = useMembers(slug);
  const { data: sessions } = useStaffSessions(slug, range.from, range.to);

  const upcoming = (sessions ?? []).filter((s) => !s.isCancelled);
  const booked = upcoming.reduce((sum, s) => sum + s.bookedCount, 0);
  const capacity = upcoming.reduce((sum, s) => sum + s.capacity, 0);

  return (
    <div className="space-y-8">
      <PageHeader title={t("admin.dashboard.title")} subtitle={t("admin.dashboard.subtitle")} />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label={t("admin.dashboard.mrr")}
          value={billing ? formatPrice(billing.monthlyRecurringRevenue, gym.currency, i18n.language) : "–"}
          hint={t("admin.dashboard.mrrHint")}
        />
        <StatCard label={t("admin.dashboard.paying")} value={billing?.activeMembers ?? "–"} />
        <StatCard label={t("admin.dashboard.members")} value={members?.filter((m) => m.role === "Member").length ?? "–"} />
        <StatCard
          label={t("admin.dashboard.fill")}
          value={capacity ? `${Math.round((booked / capacity) * 100)}%` : "–"}
          hint={t("admin.dashboard.fillHint", { booked, count: upcoming.length })}
        />
      </div>

      <InsightsCharts slug={slug} />

      <div className="grid gap-6 xl:grid-cols-2">
        <Card className="p-6">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-lg font-semibold">{t("admin.dashboard.nextSessions")}</h2>
            <Link to={`/g/${slug}/admin/sessions`} className="text-sm font-semibold text-accent hover:underline">
              {t("admin.dashboard.viewAll")}
            </Link>
          </div>
          {!upcoming.length ? (
            <p className="text-sm text-muted">{t("schedule.empty")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {upcoming.slice(0, 6).map((s) => (
                <li key={s.id} className="flex items-center justify-between gap-3 py-3">
                  <div>
                    <p className="font-medium">{s.className}</p>
                    <p className="text-xs text-muted">
                      {formatShortDate(s.startsAt, i18n.language, gym.timeZone)} · {formatTime(s.startsAt, i18n.language, gym.timeZone)}
                      {s.trainerName && ` · ${s.trainerName}`}
                    </p>
                  </div>
                  <span className="text-sm font-semibold tabular-nums">
                    {s.bookedCount}/{s.capacity}
                    {s.waitlistCount > 0 && <span className="ml-1 text-xs text-warning">+{s.waitlistCount}</span>}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card className="p-6">
          <h2 className="mb-4 text-lg font-semibold">{t("admin.dashboard.subscriptions")}</h2>
          {isLoading ? (
            <Skeleton className="h-32" />
          ) : !billing?.subscriptions.length ? (
            <EmptyState icon={CreditCard} text={t("admin.dashboard.noSubscriptions")} />
          ) : (
            <ul className="divide-y divide-line">
              {billing.subscriptions.slice(0, 8).map((s) => (
                <li key={s.id} className="flex items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{s.memberName}</p>
                    <p className="truncate text-xs text-muted">
                      {s.planName} · {formatPrice(s.price, gym.currency, i18n.language)} / {t(`plans.per.${s.interval}`)}
                      {s.currentPeriodEnd && ` · ${formatDate(s.currentPeriodEnd, i18n.language)}`}
                    </p>
                  </div>
                  <SubscriptionBadge status={s.status} />
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  );
}
