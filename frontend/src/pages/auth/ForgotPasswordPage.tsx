import { zodResolver } from "@hookform/resolvers/zod";
import { MailCheck } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Field, Input } from "@/components/ui/Input";
import { errorMessage } from "@/i18n";
import { api } from "@/lib/api";
import { forgotPasswordSchema } from "@/lib/validation";
import { AuthLayout } from "./AuthLayout";

type ForgotForm = z.infer<typeof forgotPasswordSchema>;

export default function ForgotPasswordPage() {
  const { t } = useTranslation();
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotForm>({ resolver: zodResolver(forgotPasswordSchema) });

  const onSubmit = async ({ email }: ForgotForm) => {
    setError(null);
    try {
      await api.post("/api/auth/forgot-password", { email });
      setSentTo(email.trim());
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  if (sentTo) {
    return (
      <AuthLayout title={t("auth.forgotSentTitle")} subtitle={t("auth.forgotSentText", { email: sentTo })}>
        <div className="space-y-5">
          <div className="grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent">
            <MailCheck className="size-6" />
          </div>
          <Link to="/login" className="inline-block text-sm font-semibold text-accent hover:underline">
            {t("auth.backToLogin")}
          </Link>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title={t("auth.forgotTitle")} subtitle={t("auth.forgotSubtitle")}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
        {error && (
          <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        )}
        <Field label={t("auth.email")} error={errors.email?.message}>
          {(id, d) => <Input id={id} type="email" autoComplete="email" aria-describedby={d} aria-invalid={!!errors.email} {...register("email")} />}
        </Field>
        <Button type="submit" size="lg" className="w-full" loading={isSubmitting}>
          {t("auth.forgotButton")}
        </Button>
        <p className="text-center text-sm">
          <Link to="/login" className="font-semibold text-accent hover:underline">
            {t("auth.backToLogin")}
          </Link>
        </p>
      </form>
    </AuthLayout>
  );
}
