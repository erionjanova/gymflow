import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo } from "react";
import { Controller, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { z } from "zod";
import { keys } from "@/api/queries";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field, Input, Select, Textarea } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import { Switch } from "@/components/ui/Switch";
import { errorMessage } from "@/i18n";
import { api, gymPath } from "@/lib/api";
import { gymSettingsSchema } from "@/lib/validation";
import { useGymContext } from "@/providers/GymProvider";

type SettingsForm = z.infer<typeof gymSettingsSchema>;

const SWATCHES = ["#65a30d", "#dc2626", "#2563eb", "#7c3aed", "#ea580c", "#0d9488", "#db2777", "#18181b"];

export default function SettingsPage() {
  const { t } = useTranslation();
  const { gym, slug } = useGymContext();
  const queryClient = useQueryClient();

  const timeZones = useMemo(() => {
    try {
      return Intl.supportedValuesOf("timeZone");
    } catch {
      return [gym.timeZone];
    }
  }, [gym.timeZone]);

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    formState: { errors, isSubmitting, isDirty },
    reset,
  } = useForm<SettingsForm>({
    resolver: zodResolver(gymSettingsSchema),
    defaultValues: {
      name: gym.name,
      description: gym.description ?? "",
      address: gym.address ?? "",
      phone: gym.phone ?? "",
      brandColor: gym.brandColor,
      currency: gym.currency as SettingsForm["currency"],
      timeZone: gym.timeZone,
      requireActiveMembership: gym.requireActiveMembership,
    },
  });

  const color = watch("brandColor");

  const onSubmit = async (data: SettingsForm) => {
    try {
      await api.put(gymPath(slug, "/settings"), {
        ...data,
        description: data.description || null,
        address: data.address || null,
        phone: data.phone || null,
      });
      toast.success(t("common.saved"));
      reset(data);
      queryClient.invalidateQueries({ queryKey: keys.gym(slug) });
      queryClient.invalidateQueries({ queryKey: ["my-gyms"] });
    } catch (e) {
      toast.error(errorMessage(e));
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader title={t("settings.title")} subtitle={t("settings.subtitle")} />

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6" noValidate>
        <Card className="space-y-5 p-6">
          <h2 className="text-lg font-semibold">{t("settings.profile")}</h2>
          <Field label={t("createGym.name")} error={errors.name?.message}>
            {(id, d) => <Input id={id} aria-describedby={d} aria-invalid={!!errors.name} {...register("name")} />}
          </Field>
          <Field label={t("settings.description")} error={errors.description?.message} optional>
            {(id, d) => <Textarea id={id} aria-describedby={d} {...register("description")} />}
          </Field>
          <div className="grid gap-5 sm:grid-cols-2">
            <Field label={t("settings.address")} error={errors.address?.message} optional>
              {(id, d) => <Input id={id} aria-describedby={d} {...register("address")} />}
            </Field>
            <Field label={t("settings.phone")} error={errors.phone?.message} optional>
              {(id, d) => <Input id={id} type="tel" aria-describedby={d} aria-invalid={!!errors.phone} {...register("phone")} />}
            </Field>
          </div>
          <p className="text-xs text-muted">{t("settings.slugNote", { slug })}</p>
        </Card>

        <Card className="space-y-5 p-6">
          <h2 className="text-lg font-semibold">{t("settings.brand")}</h2>
          <div className="flex flex-wrap items-center gap-3">
            {SWATCHES.map((s) => (
              <button
                key={s}
                type="button"
                aria-label={s}
                aria-pressed={color.toLowerCase() === s}
                onClick={() => setValue("brandColor", s, { shouldDirty: true })}
                className="size-9 cursor-pointer rounded-full ring-offset-2 ring-offset-surface transition-transform hover:scale-110 aria-pressed:ring-2 aria-pressed:ring-fg"
                style={{ backgroundColor: s }}
              />
            ))}
            <label className="flex items-center gap-2 text-sm text-muted">
              <input
                type="color"
                value={color}
                onChange={(e) => setValue("brandColor", e.target.value, { shouldDirty: true })}
                className="size-9 cursor-pointer rounded-full border border-line bg-transparent"
              />
              {color}
            </label>
          </div>
          {errors.brandColor && <p className="text-xs text-danger">{t(errors.brandColor.message as "validation.color")}</p>}
        </Card>

        <Card className="space-y-5 p-6">
          <h2 className="text-lg font-semibold">{t("settings.operations")}</h2>
          <div className="grid gap-5 sm:grid-cols-2">
            <Field label={t("settings.currency")}>
              {(id) => (
                <Select id={id} {...register("currency")}>
                  {["EUR", "USD", "GBP", "CHF"].map((c) => (
                    <option key={c}>{c}</option>
                  ))}
                </Select>
              )}
            </Field>
            <Field label={t("settings.timeZone")} hint={t("settings.timeZoneHint")}>
              {(id, d) => (
                <Select id={id} aria-describedby={d} {...register("timeZone")}>
                  {timeZones.map((z) => (
                    <option key={z}>{z}</option>
                  ))}
                </Select>
              )}
            </Field>
          </div>
          <Controller
            control={control}
            name="requireActiveMembership"
            render={({ field }) => (
              <label className="flex items-start justify-between gap-4 rounded-xl border border-line px-4 py-3">
                <span>
                  <span className="block text-sm font-medium">{t("settings.requireMembership")}</span>
                  <span className="mt-0.5 block text-xs text-muted">{t("settings.requireMembershipHint")}</span>
                </span>
                <Switch checked={field.value} onChange={field.onChange} label={t("settings.requireMembership")} />
              </label>
            )}
          />
        </Card>

        <div className="flex justify-end">
          <Button type="submit" size="lg" loading={isSubmitting} disabled={!isDirty}>
            {t("common.save")}
          </Button>
        </div>
      </form>
    </div>
  );
}
