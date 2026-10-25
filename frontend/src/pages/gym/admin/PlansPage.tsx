import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Tags, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { z } from "zod";
import { keys, usePlans } from "@/api/queries";
import { PlanCard } from "@/components/gym/PlanCard";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { Field, Input, Select, Textarea } from "@/components/ui/Input";
import { ConfirmDialog, Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { Switch } from "@/components/ui/Switch";
import { errorMessage } from "@/i18n";
import { api, gymPath } from "@/lib/api";
import type { Plan } from "@/lib/types";
import { planSchema } from "@/lib/validation";
import { useGymContext } from "@/providers/GymProvider";

type PlanForm = z.infer<typeof planSchema>;

const emptyForm: PlanForm = { name: "", description: "", price: 30, interval: "Month", isActive: true, sortOrder: 0 };

function PlanModal({ open, onClose, plan }: { open: boolean; onClose: () => void; plan: Plan | null }) {
  const { t } = useTranslation();
  const { slug, gym } = useGymContext();
  const queryClient = useQueryClient();

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<PlanForm>({ resolver: zodResolver(planSchema), defaultValues: emptyForm });

  useEffect(() => {
    if (open) reset(plan ? { ...plan, description: plan.description ?? "" } : emptyForm);
  }, [open, plan, reset]);

  const onSubmit = async (data: PlanForm) => {
    try {
      const body = { ...data, description: data.description || null };
      if (plan) await api.put(gymPath(slug, `/admin/plans/${plan.id}`), body);
      else await api.post(gymPath(slug, "/admin/plans"), body);
      toast.success(t("common.saved"));
      queryClient.invalidateQueries({ queryKey: keys.plans(slug) });
      onClose();
    } catch (e) {
      toast.error(errorMessage(e));
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={plan ? t("admin.plans.edit") : t("admin.plans.new")}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button onClick={handleSubmit(onSubmit)} loading={isSubmitting}>
            {t("common.save")}
          </Button>
        </>
      }
    >
      <form className="space-y-5" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Field label={t("admin.plans.name")} error={errors.name?.message}>
          {(id, d) => <Input id={id} placeholder="Premium" aria-describedby={d} aria-invalid={!!errors.name} {...register("name")} />}
        </Field>
        <Field label={t("admin.plans.description")} error={errors.description?.message} optional>
          {(id, d) => <Textarea id={id} aria-describedby={d} {...register("description")} />}
        </Field>
        <div className="grid gap-5 sm:grid-cols-3">
          <Field label={`${t("admin.plans.price")} (${gym.currency})`} error={errors.price?.message}>
            {(id, d) => <Input id={id} type="number" min={0} step="0.01" aria-describedby={d} {...register("price", { valueAsNumber: true })} />}
          </Field>
          <Field label={t("admin.plans.interval")}>
            {(id) => (
              <Select id={id} {...register("interval")}>
                <option value="Month">{t("plans.interval.Month")}</option>
                <option value="Year">{t("plans.interval.Year")}</option>
              </Select>
            )}
          </Field>
          <Field label={t("admin.plans.order")} error={errors.sortOrder?.message}>
            {(id, d) => <Input id={id} type="number" min={0} aria-describedby={d} {...register("sortOrder", { valueAsNumber: true })} />}
          </Field>
        </div>
        <Controller
          control={control}
          name="isActive"
          render={({ field }) => (
            <label className="flex items-center justify-between gap-4 rounded-xl border border-line px-4 py-3">
              <span className="text-sm font-medium">{t("admin.plans.visible")}</span>
              <Switch checked={field.value} onChange={field.onChange} label={t("admin.plans.visible")} />
            </label>
          )}
        />
      </form>
    </Modal>
  );
}

export default function PlansPage() {
  const { t } = useTranslation();
  const { slug } = useGymContext();
  const queryClient = useQueryClient();
  const { data: plans, isLoading } = usePlans(slug, true);
  const [modal, setModal] = useState<{ open: boolean; plan: Plan | null }>({ open: false, plan: null });
  const [deleting, setDeleting] = useState<Plan | null>(null);

  const remove = useMutation({
    mutationFn: (id: string) => api.delete(gymPath(slug, `/admin/plans/${id}`)),
    onSuccess: () => {
      toast(t("admin.plans.removed"));
      queryClient.invalidateQueries({ queryKey: keys.plans(slug) });
      setDeleting(null);
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("admin.plans.title")}
        subtitle={t("admin.plans.subtitle")}
        actions={
          <Button onClick={() => setModal({ open: true, plan: null })}>
            <Plus className="size-4" />
            {t("admin.plans.new")}
          </Button>
        }
      />

      {isLoading ? (
        <Skeleton className="h-40" />
      ) : !plans?.length ? (
        <EmptyState icon={Tags} text={t("admin.plans.empty")} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {plans.map((p) => (
            <div key={p.id} className="relative">
              {!p.isActive && <Badge className="absolute top-4 right-4">{t("admin.inactive")}</Badge>}
              <PlanCard
                plan={p}
                action={
                  <div className="flex gap-1">
                    <Button variant="ghost" size="sm" onClick={() => setModal({ open: true, plan: p })}>
                      <Pencil className="size-4" />
                      {t("common.edit")}
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => setDeleting(p)}>
                      <Trash2 className="size-4" />
                      {t("common.delete")}
                    </Button>
                  </div>
                }
              />
            </div>
          ))}
        </div>
      )}

      <PlanModal open={modal.open} plan={modal.plan} onClose={() => setModal({ open: false, plan: null })} />
      <ConfirmDialog
        open={!!deleting}
        title={t("admin.plans.deleteTitle")}
        description={t("admin.plans.deleteText")}
        confirmLabel={t("common.delete")}
        destructive
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}
