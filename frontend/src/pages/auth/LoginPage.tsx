import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2 } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate } from "react-router";
import { toast } from "sonner";
import type { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Field, Input } from "@/components/ui/Input";
import { errorMessage } from "@/i18n";
import { loginSchema } from "@/lib/validation";
import { useAuth } from "@/providers/AuthProvider";
import { AuthLayout } from "./AuthLayout";

type LoginForm = z.infer<typeof loginSchema>;

const DEMO_ACCOUNTS = [
  { role: "admin", name: "Erion Janova", email: "admin@gymflow.local", password: "Admin12345" },
  { role: "owner", name: "Driton Berisha", email: "owner@powergym.local", password: "Owner12345" },
  { role: "trainer", name: "Elira Shala", email: "trainer@powergym.local", password: "Trainer12345" },
  { role: "member", name: "Valon Morina", email: "member@powergym.local", password: "Member12345" },
] as const;

export default function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const [demoLoading, setDemoLoading] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(loginSchema) });

  const onSubmit = async (data: LoginForm) => {
    setError(null);
    try {
      const user = await login(data.email, data.password);
      toast.success(t("auth.welcome", { name: user.fullName.split(" ")[0] }));
      const from = (location.state as { from?: string } | null)?.from;
      navigate(from && from !== "/login" ? from : user.isPlatformAdmin ? "/platform" : "/gyms", { replace: true });
    } catch (e) {
      setError(errorMessage(e));
    }
  };

  const loginAsDemo = async (account: (typeof DEMO_ACCOUNTS)[number]) => {
    setDemoLoading(account.email);
    await onSubmit({ email: account.email, password: account.password });
    setDemoLoading(null);
  };

  return (
    <AuthLayout title={t("auth.loginTitle")} subtitle={t("auth.loginSubtitle")}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
        {error && (
          <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft px-4 py-3 text-sm text-danger">
            {error}
          </div>
        )}
        <Field label={t("auth.email")} error={errors.email?.message}>
          {(id, d) => <Input id={id} type="email" autoComplete="email" aria-describedby={d} aria-invalid={!!errors.email} {...register("email")} />}
        </Field>
        <Field label={t("auth.password")} error={errors.password?.message}>
          {(id, d) => (
            <Input id={id} type="password" autoComplete="current-password" aria-describedby={d} aria-invalid={!!errors.password} {...register("password")} />
          )}
        </Field>
        <div className="-mt-2 text-right">
          <Link to="/forgot-password" className="inline-flex min-h-6 items-center text-sm font-medium text-muted hover:text-accent hover:underline">
            {t("auth.forgotLink")}
          </Link>
        </div>
        <Button type="submit" size="lg" className="w-full" loading={isSubmitting}>
          {t("auth.loginButton")}
        </Button>
        <p className="text-center text-sm text-muted">
          {t("auth.noAccount")}{" "}
          <Link to="/register" state={location.state} className="font-semibold text-accent hover:underline">
            {t("nav.register")}
          </Link>
        </p>
        <div className="rounded-xl border border-dashed border-line p-4">
          <p className="text-sm font-semibold">{t("auth.demoTitle")}</p>
          <p className="mt-0.5 text-xs text-muted">{t("auth.demoHint")}</p>
          <div className="mt-3 grid grid-cols-2 gap-2">
            {DEMO_ACCOUNTS.map((account) => (
              <button
                key={account.email}
                type="button"
                disabled={isSubmitting || demoLoading !== null}
                onClick={() => loginAsDemo(account)}
                className="flex cursor-pointer items-center justify-between gap-2 rounded-xl border border-line bg-surface px-3 py-2 text-left transition-colors hover:border-accent hover:bg-accent-soft disabled:cursor-default disabled:opacity-60"
              >
                <span className="min-w-0">
                  <span className="block truncate text-sm font-semibold">{t(`auth.demoRoles.${account.role}`)}</span>
                  <span className="block truncate text-xs text-muted">{account.name}</span>
                </span>
                {demoLoading === account.email && <Loader2 className="size-4 shrink-0 animate-spin text-accent" aria-hidden />}
              </button>
            ))}
          </div>
        </div>
      </form>
    </AuthLayout>
  );
}
