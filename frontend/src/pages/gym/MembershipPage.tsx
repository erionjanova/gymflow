import { useMutation, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, CreditCard, Loader2, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { toast } from "sonner";
import { keys, useMySubscription, usePlans } from "@/api/queries";
import { PlanCard } from "@/components/gym/PlanCard";
import { SubscriptionBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, Skeleton } from "@/components/ui/Card";
import { ConfirmDialog } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { api, gymPath } from "@/lib/api";
import { formatDate, formatPrice } from "@/lib/format";
import type { MySubscription, Plan } from "@/lib/types";
import { useGymContext } from "@/providers/GymProvider";

export default function MembershipPage() {
  const { t, i18n } = useTranslation();
  const { gym, slug } = useGymContext();
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const checkout = params.get("checkout");
  const sessionId = params.get("session_id");
  const [timedOut, setTimedOut] = useState(false);
  const syncedSession = useRef<string | null>(null);
  const [switchTo, setSwitchTo] = useState<Plan | null>(null);

  const awaitingPayment = checkout === "success";
  const { data: subscription, isLoading } = useMySubscription(slug, awaitingPayment && !timedOut ? 2000 : false);
  const { data: plans } = usePlans(slug);
  const hasAccess = subscription?.status === "Active" || subscription?.status === "PastDue";
  const confirming = awaitingPayment && !hasAccess && !timedOut;

  const syncCheckout = useCallback(() => {
    if (!sessionId) return;
    api
      .post<MySubscription | undefined>(gymPath(slug, "/me/subscription/sync"), { checkoutSessionId: sessionId })
      .then((s) => s && queryClient.setQueryData(keys.subscription(slug), s))
      .catch(() => {});
  }, [sessionId, slug, queryClient]);

  useEffect(() => {
    if (!awaitingPayment || !sessionId || syncedSession.current === sessionId) return;
    syncedSession.current = sessionId;
    syncCheckout();
  }, [awaitingPayment, sessionId, syncCheckout]);

  useEffect(() => {
    if (!confirming) return;
    const timer = setTimeout(() => setTimedOut(true), 45_000);
    return () => clearTimeout(timer);
  }, [confirming]);

  useEffect(() => {
    if (checkout === "cancelled") {
      toast(t("membership.checkoutCancelled"));
      setParams({}, { replace: true });
    }
  }, [checkout, setParams, t]);

  useEffect(() => {
    if (!awaitingPayment || !hasAccess) return;
    toast.success(t("membership.welcome"));
    setParams({}, { replace: true });
  }, [awaitingPayment, hasAccess, setParams, t]);

  const startCheckout = useMutation({
    mutationFn: (planId: string) => api.post<{ url: string }>(gymPath(slug, `/plans/${planId}/checkout`)),
    onSuccess: ({ url }) => window.location.assign(url),
    onError: (e) => toast.error(errorMessage(e)),
  });

  const changePlan = useMutation({
    mutationFn: (planId: string) => api.put<MySubscription>(gymPath(slug, "/me/subscription/plan"), { planId }),
    onSuccess: (s) => {
      toast.success(t("membership.switched", { plan: s.planName }));
      queryClient.setQueryData(keys.subscription(slug), s);
      setSwitchTo(null);
    },
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

      {confirming && (
        <div className="flex items-center gap-3 rounded-2xl border border-accent/30 bg-accent-soft px-5 py-4 text-sm font-medium text-accent">
          <Loader2 className="size-4 animate-spin" />
          {t("membership.confirming")}
        </div>
      )}

      {awaitingPayment && !hasAccess && timedOut && (
        <div className="flex flex-col gap-3 rounded-2xl border border-warning/30 bg-warning-soft px-5 py-4 text-sm text-warning sm:flex-row sm:items-center">
          <p className="flex-1">{t("membership.stillConfirming")}</p>
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              setTimedOut(false);
              syncCheckout();
            }}
          >
            <RefreshCw className="size-4" />
            {t("membership.checkAgain")}
          </Button>
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

      <section>
        <h2 className="text-2xl font-semibold">{hasAccess ? t("membership.changeTitle") : t("membership.choose")}</h2>
        {hasAccess && <p className="mt-1 text-sm text-muted">{t("membership.changeHint")}</p>}
        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {plans?.map((plan, i) => {
            const isCurrent = hasAccess && plan.id === subscription?.planId;
            return (
              <PlanCard
                key={plan.id}
                plan={plan}
                highlight={hasAccess ? isCurrent : i === 1}
                action={
                  !hasAccess ? (
                    <Button
                      className="w-full"
                      variant={i === 1 ? "primary" : "outline"}
                      loading={startCheckout.isPending && startCheckout.variables === plan.id}
                      disabled={startCheckout.isPending || confirming}
                      onClick={() => startCheckout.mutate(plan.id)}
                    >
                      {t("membership.subscribe")}
                    </Button>
                  ) : isCurrent ? (
                    subscription?.cancelAtPeriodEnd ? (
                      <Button className="w-full" loading={setCancel.isPending} onClick={() => setCancel.mutate(false)}>
                        {t("membership.keepThis")}
                      </Button>
                    ) : (
                      <Button className="w-full" variant="outline" disabled>
                        {t("membership.current")}
                      </Button>
                    )
                  ) : (
                    <Button
                      className="w-full"
                      variant="outline"
                      disabled={subscription?.status === "PastDue" || changePlan.isPending}
                      onClick={() => setSwitchTo(plan)}
                    >
                      {t("membership.switch")}
                    </Button>
                  )
                }
              />
            );
          })}
        </div>
        <p className="mt-4 text-xs text-muted">{t("membership.stripeNote")}</p>
      </section>

      <ConfirmDialog
        open={!!switchTo}
        title={t("membership.switchTitle", { plan: switchTo?.name })}
        description={t("membership.switchText", {
          plan: switchTo?.name,
          price: switchTo ? `${formatPrice(switchTo.price, gym.currency, i18n.language)} / ${t(`plans.per.${switchTo.interval}`)}` : "",
        })}
        confirmLabel={t("membership.switchConfirm")}
        loading={changePlan.isPending}
        onConfirm={() => switchTo && changePlan.mutate(switchTo.id)}
        onClose={() => setSwitchTo(null)}
      />
    </div>
  );
}
