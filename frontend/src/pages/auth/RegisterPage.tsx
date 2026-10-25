import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate } from "react-router";
import { toast } from "sonner";
import type { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Field, Input } from "@/components/ui/Input";
import { currentLanguage, errorMessage } from "@/i18n";
import { registerSchema } from "@/lib/validation";
import { useAuth } from "@/providers/AuthProvider";
import { AuthLayout } from "./AuthLayout";

type RegisterForm = z.infer<typeof registerSchema>;

export default function RegisterPage() {
  const { t } = useTranslation();
  const { register: registerUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterForm>({ resolver: zodResolver(registerSchema) });

  const onSubmit = async (data: RegisterForm) => {
    setError(null);
    try {
      const user = await registerUser({ ...data, preferredLanguage: currentLanguage() });
      toast.success(t("auth.welcome", { name: user.fullName.split(" ")[0] }));
      const from = (location.state as { from?: string } | null)?.from;
      navigate(from && from !== "/register" ? from : "/gyms", { replace: true });
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  return (
    <AuthLayout title={t("auth.registerTitle")} subtitle={t("auth.registerSubtitle")}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
        {error && (
          <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        )}
        <Field label={t("auth.fullName")} error={errors.fullName?.message}>
          {(id, d) => <Input id={id} autoComplete="name" aria-describedby={d} aria-invalid={!!errors.fullName} {...register("fullName")} />}
        </Field>
        <Field label={t("auth.email")} error={errors.email?.message}>
          {(id, d) => <Input id={id} type="email" autoComplete="email" aria-describedby={d} aria-invalid={!!errors.email} {...register("email")} />}
        </Field>
        <Field label={t("auth.password")} error={errors.password?.message} hint={t("auth.passwordHint")}>
          {(id, d) => (
            <Input id={id} type="password" autoComplete="new-password" aria-describedby={d} aria-invalid={!!errors.password} {...register("password")} />
          )}
        </Field>
        <Button type="submit" size="lg" className="w-full" loading={isSubmitting}>
          {t("auth.registerButton")}
        </Button>
        <p className="text-center text-sm text-muted">
          {t("auth.haveAccount")}{" "}
          <Link to="/login" state={location.state} className="font-semibold text-accent hover:underline">
            {t("nav.login")}
          </Link>
        </p>
      </form>
    </AuthLayout>
  );
}
