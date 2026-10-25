import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useNavigate, useParams } from "react-router";
import { toast } from "sonner";
import type { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Field, Input } from "@/components/ui/Input";
import { errorMessage } from "@/i18n";
import { api, ApiError } from "@/lib/api";
import { resetPasswordSchema } from "@/lib/validation";
import { AuthLayout } from "./AuthLayout";

type ResetForm = z.infer<typeof resetPasswordSchema>;

export default function ResetPasswordPage() {
  const { t } = useTranslation();
  const { token = "" } = useParams();
  const navigate = useNavigate();
  const [error, setError] = useState<{ message: string; invalidLink: boolean } | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetForm>({ resolver: zodResolver(resetPasswordSchema) });

  const onSubmit = async ({ newPassword }: ResetForm) => {
    setError(null);
    try {
      await api.post("/api/auth/reset-password", { token, newPassword });
      toast.success(t("auth.resetDone"));
      navigate("/login", { replace: true });
    } catch (e) {
      setError({ message: errorMessage(e), invalidLink: e instanceof ApiError && e.code === "reset_link_invalid" });
    }
  };

  return (
    <AuthLayout title={t("auth.resetTitle")} subtitle={t("auth.resetSubtitle")}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
        {error && (
          <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft px-4 py-3 text-sm text-danger">
            {error.message}
            {error.invalidLink && (
              <Link to="/forgot-password" className="mt-1 block font-semibold underline">
                {t("auth.requestNew")}
              </Link>
            )}
          </div>
        )}
        <Field label={t("account.newPassword")} error={errors.newPassword?.message} hint={t("auth.passwordHint")}>
          {(id, d) => (
            <Input id={id} type="password" autoComplete="new-password" aria-describedby={d} aria-invalid={!!errors.newPassword} {...register("newPassword")} />
          )}
        </Field>
        <Field label={t("account.confirmPassword")} error={errors.confirmPassword?.message}>
          {(id, d) => (
            <Input
              id={id}
              type="password"
              autoComplete="new-password"
              aria-describedby={d}
              aria-invalid={!!errors.confirmPassword}
              {...register("confirmPassword")}
            />
          )}
        </Field>
        <Button type="submit" size="lg" className="w-full" loading={isSubmitting}>
          {t("auth.resetButton")}
        </Button>
      </form>
    </AuthLayout>
  );
}
