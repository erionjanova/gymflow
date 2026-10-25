import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ExternalLink } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { toast } from "sonner";
import { keys, usePlatformGyms, usePlatformStats } from "@/api/queries";
import { Badge } from "@/components/ui/Badge";
import { Card, Skeleton } from "@/components/ui/Card";
import { PageHeader, StatCard } from "@/components/ui/PageHeader";
import { Switch } from "@/components/ui/Switch";
import { errorMessage } from "@/i18n";
import { api } from "@/lib/api";
import { formatDate } from "@/lib/format";

export default function PlatformPage() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const { data: stats } = usePlatformStats();
  const { data: gyms, isLoading } = usePlatformGyms();

  const suspend = useMutation({
    mutationFn: ({ id, isSuspended }: { id: string; isSuspended: boolean }) =>
      api.put(`/api/platform/gyms/${id}/suspension`, { isSuspended }),
    onSuccess: (_, v) => {
      toast.success(v.isSuspended ? t("platform.suspended") : t("platform.reactivated"));
      queryClient.invalidateQueries({ queryKey: keys.platform });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 lg:px-10">
      <PageHeader title={t("platform.title")} subtitle={t("platform.subtitle")} />

      <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label={t("platform.stats.gyms")} value={stats?.gyms ?? "–"} />
        <StatCard label={t("platform.stats.activeGyms")} value={stats?.activeGyms ?? "–"} />
        <StatCard label={t("platform.stats.users")} value={stats?.users ?? "–"} />
        <StatCard label={t("platform.stats.memberships")} value={stats?.memberships ?? "–"} />
      </div>

      <Card className="mt-8 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-line bg-surface-2 text-xs tracking-wide text-muted uppercase">
              <tr>
                <th className="px-5 py-3 font-semibold">{t("platform.table.gym")}</th>
                <th className="px-5 py-3 font-semibold">{t("platform.table.owner")}</th>
                <th className="px-5 py-3 font-semibold">{t("platform.table.members")}</th>
                <th className="px-5 py-3 font-semibold">{t("platform.table.created")}</th>
                <th className="px-5 py-3 font-semibold">{t("platform.table.active")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {isLoading &&
                [0, 1, 2].map((i) => (
                  <tr key={i}>
                    <td colSpan={5} className="px-5 py-3">
                      <Skeleton className="h-6" />
                    </td>
                  </tr>
                ))}
              {gyms?.map((g) => (
                <tr key={g.id}>
                  <td className="px-5 py-3">
                    <Link to={`/g/${g.slug}`} className="inline-flex items-center gap-1.5 font-semibold hover:text-accent">
                      {g.name}
                      <ExternalLink className="size-3.5" />
                    </Link>
                    <p className="text-xs text-muted">/g/{g.slug}</p>
                  </td>
                  <td className="px-5 py-3 text-muted">{g.ownerEmail ?? "–"}</td>
                  <td className="px-5 py-3 tabular-nums">{g.memberCount}</td>
                  <td className="px-5 py-3 text-muted">{formatDate(g.createdAt, i18n.language)}</td>
                  <td className="px-5 py-3">
                    <div className="flex items-center gap-3">
                      <Switch
                        checked={!g.isSuspended}
                        label={t("platform.table.active")}
                        onChange={(active) => suspend.mutate({ id: g.id, isSuspended: !active })}
                      />
                      {g.isSuspended && <Badge tone="danger">{t("platform.suspendedBadge")}</Badge>}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}
