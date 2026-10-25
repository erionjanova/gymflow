import { ArrowRight, Dumbbell, Plus } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useMyGyms } from "@/api/queries";
import { RoleBadge } from "@/components/ui/Badge";
import { buttonClass } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Card";
import { EmptyState, ErrorState } from "@/components/ui/EmptyState";
import { PageHeader } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { useAuth } from "@/providers/AuthProvider";

export default function MyGymsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { data: gyms, isLoading, error, refetch } = useMyGyms();

  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6 lg:px-10">
      <PageHeader
        title={t("myGyms.title", { name: user?.fullName.split(" ")[0] })}
        subtitle={t("myGyms.subtitle")}
        actions={
          <Link to="/gyms/new" className={buttonClass("primary")}>
            <Plus className="size-4" />
            {t("nav.createGym")}
          </Link>
        }
      />

      <div className="mt-8">
        {isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <Skeleton className="h-28" />
            <Skeleton className="h-28" />
          </div>
        ) : error ? (
          <ErrorState message={errorMessage(error)} onRetry={() => refetch()} retryLabel={t("common.retry")} />
        ) : !gyms?.length ? (
          <EmptyState
            icon={Dumbbell}
            text={t("myGyms.empty")}
            action={
              <Link to="/g/powergym" className={buttonClass("outline", "sm")}>
                {t("myGyms.visitDemo")}
              </Link>
            }
          />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            {gyms.map((gym) => (
              <Link
                key={gym.id}
                to={gym.role === "Member" ? `/g/${gym.slug}` : `/g/${gym.slug}/admin`}
                className="group flex items-center gap-4 rounded-2xl border border-line bg-surface p-5 transition-all hover:-translate-y-0.5 hover:shadow-lg hover:shadow-black/5"
              >
                <span
                  className="grid size-12 shrink-0 place-items-center rounded-xl font-display text-xl font-bold text-white"
                  style={{ backgroundColor: gym.brandColor }}
                >
                  {gym.name.charAt(0)}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-lg font-semibold">{gym.name}</p>
                  <div className="mt-1 flex items-center gap-2">
                    <RoleBadge role={gym.role} />
                    <span className="truncate text-xs text-muted">/g/{gym.slug}</span>
                  </div>
                </div>
                <ArrowRight className="size-5 text-muted transition-transform group-hover:translate-x-1" />
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
