import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import type { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field, Input, Select } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { api, ApiError } from "@/lib/api";
import type { GymMembership } from "@/lib/types";
import { createGymSchema } from "@/lib/validation";

type CreateGymForm = z.infer<typeof createGymSchema>;

const suggestSlug = (name: string) =>
  name
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 40);

export default function CreateGymPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    setError: setFieldError,
    formState: { errors, isSubmitting },
  } = useForm<CreateGymForm>({ resolver: zodResolver(createGymSchema), defaultValues: { currency: "EUR", slug: "" } });

  const name = watch("name") ?? "";
  const slug = watch("slug");
  const previewSlug = slug || suggestSlug(name) || "your-gym";

  const onSubmit = async (data: CreateGymForm) => {
    setError(null);
    try {
      const result = await api.post<GymMembership>("/api/gyms", {
        ...data,
        slug: data.slug || null,
        address: data.address || null,
        phone: data.phone || null,
      });
      await queryClient.invalidateQueries({ queryKey: ["my-gyms"] });
      toast.success(t("createGym.created", { name: result.gym.name }));
      navigate(`/g/${result.gym.slug}/admin`, { replace: true });
    } catch (e) {
      if (e instanceof ApiError && e.code === "slug_taken") setFieldError("slug", { message: "validation.slugTaken" });
      else setError(errorMessage(e));
    }
  };

  return (
    <div className="mx-auto max-w-2xl px-4 py-10 sm:px-6">
      <PageHeader title={t("createGym.title")} subtitle={t("createGym.subtitle")} />

      <Card className="mt-8 p-6 sm:p-8">
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
          {error && (
            <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft px-4 py-3 text-sm text-danger">
              {error}
            </div>
          )}
          <Field label={t("createGym.name")} error={errors.name?.message}>
            {(id, d) => <Input id={id} placeholder="Power Gym" aria-describedby={d} aria-invalid={!!errors.name} {...register("name")} />}
          </Field>
          <Field label={t("createGym.slug")} error={errors.slug?.message} hint={`gymflow.app/g/${previewSlug}`} optional>
            {(id, d) => (
              <Input id={id} placeholder={suggestSlug(name) || "power-gym"} aria-describedby={d} aria-invalid={!!errors.slug} {...register("slug")} />
            )}
          </Field>
          <div className="grid gap-5 sm:grid-cols-2">
            <Field label={t("settings.address")} error={errors.address?.message} optional>
              {(id, d) => <Input id={id} aria-describedby={d} {...register("address")} />}
            </Field>
            <Field label={t("settings.phone")} error={errors.phone?.message} optional>
              {(id, d) => <Input id={id} type="tel" aria-describedby={d} aria-invalid={!!errors.phone} {...register("phone")} />}
            </Field>
          </div>
          <Field label={t("settings.currency")}>
            {(id) => (
              <Select id={id} {...register("currency")}>
                {["EUR", "USD", "GBP", "CHF"].map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </Select>
            )}
          </Field>
          <Button type="submit" size="lg" className="w-full" loading={isSubmitting}>
            {t("createGym.submit")}
          </Button>
        </form>
      </Card>
    </div>
  );
}
