import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Dumbbell, Pencil, Plus, Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { z } from "zod";
import { keys, useClasses } from "@/api/queries";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, Skeleton } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { Field, Input, Textarea } from "@/components/ui/Input";
import { ConfirmDialog, Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { Switch } from "@/components/ui/Switch";
import { errorMessage } from "@/i18n";
import { api, gymPath } from "@/lib/api";
import type { GymClass } from "@/lib/types";
import { classSchema } from "@/lib/validation";
import { useGymContext } from "@/providers/GymProvider";

type ClassForm = z.infer<typeof classSchema>;

const emptyForm: ClassForm = { name: "", description: "", durationMinutes: 60, defaultCapacity: 15, isActive: true };

function ClassModal({ open, onClose, gymClass }: { open: boolean; onClose: () => void; gymClass: GymClass | null }) {
  const { t } = useTranslation();
  const { slug } = useGymContext();
  const queryClient = useQueryClient();

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<ClassForm>({ resolver: zodResolver(classSchema), defaultValues: emptyForm });

  useEffect(() => {
    if (open) reset(gymClass ? { ...gymClass, description: gymClass.description ?? "" } : emptyForm);
  }, [open, gymClass, reset]);

  const onSubmit = async (data: ClassForm) => {
    try {
      const body = { ...data, description: data.description || null };
      if (gymClass) await api.put(gymPath(slug, `/admin/classes/${gymClass.id}`), body);
      else await api.post(gymPath(slug, "/admin/classes"), body);
      toast.success(t("common.saved"));
      queryClient.invalidateQueries({ queryKey: keys.classes(slug) });
      onClose();
    } catch (e) {
      toast.error(errorMessage(e));
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={gymClass ? t("admin.classes.edit") : t("admin.classes.new")}
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
        <Field label={t("admin.classes.name")} error={errors.name?.message}>
          {(id, d) => <Input id={id} placeholder="Yoga Flow" aria-describedby={d} aria-invalid={!!errors.name} {...register("name")} />}
        </Field>
        <Field label={t("admin.classes.description")} error={errors.description?.message} optional>
          {(id, d) => <Textarea id={id} aria-describedby={d} {...register("description")} />}
        </Field>
        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t("admin.classes.duration")} error={errors.durationMinutes?.message}>
            {(id, d) => (
              <Input id={id} type="number" min={10} max={240} aria-describedby={d} {...register("durationMinutes", { valueAsNumber: true })} />
            )}
          </Field>
          <Field label={t("admin.classes.capacity")} error={errors.defaultCapacity?.message}>
            {(id, d) => (
              <Input id={id} type="number" min={1} max={500} aria-describedby={d} {...register("defaultCapacity", { valueAsNumber: true })} />
            )}
          </Field>
        </div>
        <Controller
          control={control}
          name="isActive"
          render={({ field }) => (
            <label className="flex items-center justify-between gap-4 rounded-xl border border-line px-4 py-3">
              <span className="text-sm font-medium">{t("admin.active")}</span>
              <Switch checked={field.value} onChange={field.onChange} label={t("admin.active")} />
            </label>
          )}
        />
      </form>
    </Modal>
  );
}

export default function ClassesPage() {
  const { t } = useTranslation();
  const { slug } = useGymContext();
  const queryClient = useQueryClient();
  const { data: classes, isLoading } = useClasses(slug, true);
  const [modal, setModal] = useState<{ open: boolean; gymClass: GymClass | null }>({ open: false, gymClass: null });
  const [deleting, setDeleting] = useState<GymClass | null>(null);

  const remove = useMutation({
    mutationFn: (id: string) => api.delete(gymPath(slug, `/admin/classes/${id}`)),
    onSuccess: () => {
      toast(t("admin.classes.removed"));
      queryClient.invalidateQueries({ queryKey: keys.classes(slug) });
      setDeleting(null);
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("admin.classes.title")}
        subtitle={t("admin.classes.subtitle")}
        actions={
          <Button onClick={() => setModal({ open: true, gymClass: null })}>
            <Plus className="size-4" />
            {t("admin.classes.new")}
          </Button>
        }
      />

      {isLoading ? (
        <Skeleton className="h-40" />
      ) : !classes?.length ? (
        <EmptyState icon={Dumbbell} text={t("admin.classes.empty")} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {classes.map((c) => (
            <Card key={c.id} className="flex flex-col p-5">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-lg font-semibold">{c.name}</p>
                  <p className="text-xs text-muted">
                    {t("common.minutes", { count: c.durationMinutes })} · {t("gymHome.capacity", { count: c.defaultCapacity })}
                  </p>
                </div>
                {!c.isActive && <Badge>{t("admin.inactive")}</Badge>}
              </div>
              {c.description && <p className="mt-3 flex-1 text-sm text-muted">{c.description}</p>}
              <div className="mt-4 flex gap-1">
                <Button variant="ghost" size="sm" onClick={() => setModal({ open: true, gymClass: c })}>
                  <Pencil className="size-4" />
                  {t("common.edit")}
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setDeleting(c)}>
                  <Trash2 className="size-4" />
                  {t("common.delete")}
                </Button>
              </div>
            </Card>
          ))}
        </div>
      )}

      <ClassModal open={modal.open} gymClass={modal.gymClass} onClose={() => setModal({ open: false, gymClass: null })} />
      <ConfirmDialog
        open={!!deleting}
        title={t("admin.classes.deleteTitle")}
        description={t("admin.classes.deleteText")}
        confirmLabel={t("common.delete")}
        destructive
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
        onClose={() => setDeleting(null)}
      />
    </div>
  );
}
