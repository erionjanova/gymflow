import { useTranslation } from "react-i18next";
import { useInsights } from "@/api/queries";
import { AreaTrend, BarList, ChartCard, ChartTable, StackedColumns, type ChartSeries } from "@/components/charts/Charts";
import { Skeleton } from "@/components/ui/Card";
import { formatCalendarDate, formatPrice, localeOf } from "@/lib/format";
import { cn } from "@/lib/utils";

export function InsightsCharts({ slug }: { slug: string }) {
  const { t, i18n } = useTranslation();
  const lang = i18n.language;
  const { data, isLoading, isFetching } = useInsights(slug);

  if (isLoading || !data) {
    return (
      <div className="grid gap-6 xl:grid-cols-2">
        <Skeleton className="h-80" />
        <Skeleton className="h-80" />
      </div>
    );
  }

  const money = (value: number) => formatPrice(value, data.currency, lang);
  const percent = new Intl.NumberFormat(localeOf(lang), { style: "percent", maximumFractionDigits: 0 });
  const monthLabel = (date: string) => formatCalendarDate(date, lang, { month: "short" });
  const monthTitle = (date: string) => formatCalendarDate(date, lang, { month: "long", year: "numeric" });
  const weekLabel = (date: string) => formatCalendarDate(date, lang, { day: "numeric", month: "short" });

  const revenueSeries: ChartSeries = { key: "mrr", label: t("admin.insights.revenueSeries"), color: "var(--series-1)" };
  const bookingSeries: ChartSeries[] = [
    { key: "attended", label: t("admin.insights.attended"), color: "var(--series-1)" },
    { key: "noShow", label: t("admin.insights.noShow"), color: "var(--series-2)" },
    { key: "booked", label: t("admin.insights.upcoming"), color: "var(--series-3)" },
  ];

  return (
    <div className={cn("space-y-6 transition-opacity", isFetching && "opacity-60")}>
      <div className="grid gap-6 xl:grid-cols-2">
        <ChartCard
          title={t("admin.insights.revenueTitle")}
          subtitle={t("admin.insights.revenueSubtitle")}
          table={
            <ChartTable
              head={[t("admin.insights.month"), t("admin.insights.revenueSeries")]}
              rows={data.revenue.map((p) => [monthTitle(p.month), money(p.monthlyRecurringRevenue)])}
            />
          }
        >
          <AreaTrend
            series={revenueSeries}
            format={money}
            points={data.revenue.map((p) => ({
              key: p.month,
              label: monthLabel(p.month),
              title: monthTitle(p.month),
              value: p.monthlyRecurringRevenue,
            }))}
          />
        </ChartCard>

        <ChartCard
          title={t("admin.insights.bookingsTitle")}
          subtitle={t("admin.insights.bookingsSubtitle")}
          table={
            <ChartTable
              head={[t("admin.insights.week"), ...bookingSeries.map((s) => s.label), t("admin.insights.total")]}
              rows={data.weeklyBookings.map((w) => [
                t("admin.insights.weekOf", { date: weekLabel(w.weekStart) }),
                w.attended,
                w.noShow,
                w.booked,
                w.attended + w.noShow + w.booked,
              ])}
            />
          }
        >
          <StackedColumns
            series={bookingSeries}
            totalLabel={t("admin.insights.total")}
            columns={data.weeklyBookings.map((w) => ({
              key: w.weekStart,
              label: weekLabel(w.weekStart),
              title: t("admin.insights.weekOf", { date: weekLabel(w.weekStart) }),
              values: { attended: w.attended, noShow: w.noShow, booked: w.booked },
            }))}
          />
        </ChartCard>
      </div>

      <ChartCard
        title={t("admin.insights.fillTitle")}
        subtitle={t("admin.insights.fillSubtitle")}
        table={
          <ChartTable
            head={[t("admin.insights.className"), t("admin.insights.sessions"), t("admin.insights.fillRate")]}
            rows={data.classFill.map((c) => [c.className, c.sessions, percent.format(c.fillRate)])}
          />
        }
      >
        {data.classFill.length ? (
          <BarList
            color="var(--series-1)"
            rows={data.classFill.map((c) => ({
              key: c.className,
              label: c.className,
              value: c.fillRate,
              display: percent.format(c.fillRate),
              detail: t("admin.insights.fillDetail", { booked: c.booked, capacity: c.capacity, count: c.sessions }),
            }))}
          />
        ) : (
          <p className="text-sm text-muted">{t("admin.insights.noClasses")}</p>
        )}
      </ChartCard>
    </div>
  );
}
