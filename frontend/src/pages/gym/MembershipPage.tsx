import { useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, CreditCard, Loader2 } from "lucide-react";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { keys, useMySubscription, usePlans } from "@/api/queries";
import { PlanCard } from "@/components/gym/PlanCard";
import { SubscriptionBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, Skeleton } from "@/components/ui/Card";
import { PageHeader } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { api, gymPath } from "@/lib/api";
import { formatDate, formatPrice } from "@/lib/format";
import type { MySubscription } from "@/lib/types";
import { useGymContext } from "@/providers/GymProvider";

export default function MembershipPage() {
  const { t, i18n } = useTranslation();
  const { gym, slug } = useGymContext();
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const checkout = params.get("checkout");
  const pollCount = useRef(0);

  const { data: subscription, isLoading } = useMySubscription(slug);
  const { data: plans } = usePlans(slug);
  const hasAccess = subscription?.status === "Active" || subscription?.status === "PastDue";
  const waitingForWebhook = checkout === "success" && !hasAccess && pollCount.current < 15;

  useEffect(() => {
    if (checkout === "cancelled") {
      toast(t("membership.checkoutCancelled"));
      setParams({}, { replace: true });
    }
  }, [checkout, setParams, t]);

  useEffect(() => {
    if (checkout !== "success") return;
    if (hasAccess) {
      toast.success(t("membership.welcome"));
      setParams({}, { replace: true });
      return;
    }
    const timer = setTimeout(() => {
      pollCount.current += 1;
      queryClient.invalidateQueries({ queryKey: keys.subscription(slug) });
    }, 2000);
    return () => clearTimeout(timer);
  }, [checkout, hasAccess, subscription, queryClient, slug, setParams, t]);

  const startCheckout = useMutation({
    mutationFn: (planId: string) => api.post<{ url: string }>(gymPath(slug, `/plans/${planId}/checkout`)),
    onSuccess: ({ url }) => window.location.assign(url),
    onError: (e) => toast.error(errorMessage(e)),
  });

  const setCancel = useMutation({
    mutationFn: (cancelAtPeriodEnd: boolean) =>
      api.put<MySubscription>(gymPath(slug, "/me/subscription/cancellation"), { cancelAtPeriodEnd }),
    onSuccess: (s) => {
      toast(s.cancelAtPeriodEnd ? t("membership.willEnd") : t("membership.resumed"));
      queryClient.setQueryData(keys.subscription(slug), s);
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <div className="space-y-8">
      <PageHeader title={t("membership.title")} subtitle={t("membership.subtitle")} />

      {waitingForWebhook && (
        <div className="flex items-center gap-3 rounded-2xl border border-accent/30 bg-accent-soft px-5 py-4 text-sm font-medium text-accent">
          <Loader2 className="size-4 animate-spin" />
          {t("membership.confirming")}
        </div>
      )}

      {isLoading ? (
        <Skeleton className="h-36" />
      ) : subscription && subscription.status !== "Canceled" ? (
        <Card className="flex flex-col gap-5 p-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <span className="grid size-12 place-items-center rounded-xl bg-accent-soft text-accent">
              {hasAccess ? <CheckCircle2 className="size-6" /> : <CreditCard className="size-6" />}
            </span>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <p className="text-xl font-semibold">{subscription.planName}</p>
                <SubscriptionBadge status={subscription.status} />
              </div>
              <p className="mt-0.5 text-sm text-muted">
                {formatPrice(subscription.price, gym.currency, i18n.language)} / {t(`plans.per.${subscription.interval}`)}
                {subscription.currentPeriodEnd &&
                  ` · ${t(subscription.cancelAtPeriodEnd ? "membership.endsOn" : "membership.renewsOn", {
                    date: formatDate(subscription.currentPeriodEnd, i18n.language),
                  })}`}
              </p>
              {subscription.status === "PastDue" && <p className="mt-1 text-sm text-warning">{t("membership.pastDue")}</p>}
            </div>
          </div>
          {hasAccess && (
            <Button
              variant={subscription.cancelAtPeriodEnd ? "primary" : "outline"}
              loading={setCancel.isPending}
              onClick={() => setCancel.mutate(!subscription.cancelAtPeriodEnd)}
            >
              {subscription.cancelAtPeriodEnd ? t("membership.resume") : t("membership.cancel")}
            </Button>
          )}
        </Card>
      ) : (
        <Card className="p-6">
          <p className="font-semibold">{t("membership.none")}</p>
          <p className="mt-1 text-sm text-muted">
            {gym.requireActiveMembership ? t("membership.requiredHint") : t("membership.noneHint")}
          </p>
        </Card>
      )}

      {!hasAccess && (
        <section>
          <h2 className="mb-4 text-2xl font-semibold">{t("membership.choose")}</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {plans?.map((plan, i) => (
              <PlanCard
                key={plan.id}
                plan={plan}
                highlight={i === 1}
                action={
                  <Button
                    className="w-full"
                    variant={i === 1 ? "primary" : "outline"}
                    loading={startCheckout.isPending && startCheckout.variables === plan.id}
                    disabled={startCheckout.isPending || waitingForWebhook}
                    onClick={() => startCheckout.mutate(plan.id)}
                  >
                    {t("membership.subscribe")}
                  </Button>
                }
              />
            ))}
          </div>
          <p className="mt-4 text-xs text-muted">{t("membership.stripeNote")}</p>
        </section>
      )}
    </div>
  );
}
