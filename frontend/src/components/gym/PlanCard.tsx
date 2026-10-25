import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatPrice } from "@/lib/format";
import type { Plan } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useGymContext } from "@/providers/GymProvider";

export function PlanCard({ plan, action, highlight }: { plan: Plan; action?: ReactNode; highlight?: boolean }) {
  const { t, i18n } = useTranslation();
  const { gym } = useGymContext();

  return (
    <div className={cn("flex flex-col rounded-2xl border bg-surface p-5", highlight ? "border-accent ring-2 ring-accent/20" : "border-line")}>
      <p className="font-semibold">{plan.name}</p>
      <p className="mt-2">
        <span className="font-display text-3xl font-bold">{formatPrice(plan.price, gym.currency, i18n.language)}</span>
        <span className="text-sm text-muted"> / {t(`plans.per.${plan.interval}`)}</span>
      </p>
      {plan.description && <p className="mt-2 flex-1 text-sm text-muted">{plan.description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
