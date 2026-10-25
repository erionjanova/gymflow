import { zodResolver } from "@hookform/resolvers/zod";
import { KeyRound, Shield, UserRound } from "lucide-react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { z } from "zod";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field, Input, Select } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import i18n, { errorMessage, LANGUAGES } from "@/i18n";
import { api, ApiError } from "@/lib/api";
import type { User } from "@/lib/types";
import { initials } from "@/lib/utils";
import { changePasswordSchema, profileSchema } from "@/lib/validation";
import { useAuth } from "@/providers/AuthProvider";

type ProfileForm = z.infer<typeof profileSchema>;
type PasswordForm = z.infer<typeof changePasswordSchema>;

function SectionTitle({ icon: Icon, title, text }: { icon: typeof UserRound; title: string; text?: string }) {
  return (
    <div className="flex items-start gap-3">
      <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-accent-soft text-accent">
        <Icon className="size-5" />
      </span>
      <div>
        <h2 className="text-lg font-semibold">{title}</h2>
        {text && <p className="text-sm text-muted">{text}</p>}
      </div>
    </div>
  );
}

function ProfileCard({ user }: { user: User }) {
  const { t } = useTranslation();
  const { setUser } = useAuth();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<ProfileForm>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: user.fullName, phoneNumber: user.phoneNumber ?? "", preferredLanguage: user.preferredLanguage },
  });

  const onSubmit = async (data: ProfileForm) => {
    try {
      const updated = await api.put<User>("/api/auth/me", { ...data, phoneNumber: data.phoneNumber || null });
      setUser(updated);
      reset({ fullName: updated.fullName, phoneNumber: updated.phoneNumber ?? "", preferredLanguage: updated.preferredLanguage });
      if (updated.preferredLanguage !== i18n.resolvedLanguage) await i18n.changeLanguage(updated.preferredLanguage);
      toast.success(t("common.saved"));
    } catch (e) {
      toast.error(errorMessage(e));
    }
  };

  return (
    <Card className="p-6">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
        <SectionTitle icon={UserRound} title={t("account.details")} />
        <Field label={t("account.fullName")} error={errors.fullName?.message}>
          {(id, d) => <Input id={id} autoComplete="name" aria-describedby={d} aria-invalid={!!errors.fullName} {...register("fullName")} />}
        </Field>
        <Field label={t("account.email")} hint={t("account.emailHint")}>
          {(id, d) => <Input id={id} value={user.email} readOnly disabled aria-describedby={d} className="cursor-not-allowed opacity-70" />}
        </Field>
        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t("account.phone")} error={errors.phoneNumber?.message} optional>
            {(id, d) => (
              <Input id={id} type="tel" autoComplete="tel" aria-describedby={d} aria-invalid={!!errors.phoneNumber} {...register("phoneNumber")} />
            )}
          </Field>
          <Field label={t("account.language")}>
            {(id) => (
              <Select id={id} {...register("preferredLanguage")}>
                {LANGUAGES.map((l) => (
                  <option key={l.code} value={l.code}>
                    {l.label}
                  </option>
                ))}
              </Select>
            )}
          </Field>
        </div>
        <div className="flex justify-end">
          <Button type="submit" loading={isSubmitting} disabled={!isDirty}>
            {t("common.save")}
          </Button>
        </div>
      </form>
    </Card>
  );
}

function PasswordCard() {
  const { t } = useTranslation();

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PasswordForm>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  const onSubmit = async ({ currentPassword, newPassword }: PasswordForm) => {
    try {
      await api.put("/api/auth/me/password", { currentPassword, newPassword });
      reset();
      toast.success(t("account.passwordChanged"));
    } catch (e) {
      if (e instanceof ApiError && e.code === "wrong_password") setError("currentPassword", { message: "errors.wrong_password" });
      else toast.error(errorMessage(e));
    }
  };

  return (
    <Card className="p-6">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5" noValidate>
        <SectionTitle icon={KeyRound} title={t("account.password")} text={t("account.passwordHint")} />
        <Field label={t("account.currentPassword")} error={errors.currentPassword?.message}>
          {(id, d) => (
            <Input
              id={id}
              type="password"
              autoComplete="current-password"
              aria-describedby={d}
              aria-invalid={!!errors.currentPassword}
              {...register("currentPassword")}
            />
          )}
        </Field>
        <Field label={t("account.newPassword")} error={errors.newPassword?.message}>
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
        <div className="flex justify-end">
          <Button type="submit" variant="outline" loading={isSubmitting}>
            {t("account.changePassword")}
          </Button>
        </div>
      </form>
    </Card>
  );
}

export default function AccountPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  if (!user) return null;

  return (
    <div className="mx-auto max-w-[100rem] px-4 py-10 sm:px-6 lg:px-10 2xl:px-16">
      <PageHeader title={t("account.title")} subtitle={t("account.subtitle")} />

      <div className="mt-8 flex items-center gap-4 rounded-2xl border border-line bg-surface p-5">
        <span className="grid size-16 shrink-0 place-items-center rounded-2xl bg-accent-soft font-display text-xl font-bold text-accent">
          {initials(user.fullName)}
        </span>
        <div className="min-w-0">
          <p className="truncate text-xl font-semibold">{user.fullName}</p>
          <p className="truncate text-sm text-muted">{user.email}</p>
        </div>
        {user.isPlatformAdmin && (
          <span className="ml-auto hidden items-center gap-1.5 rounded-full bg-surface-2 px-3 py-1 text-xs font-semibold text-muted sm:inline-flex">
            <Shield className="size-3.5" />
            {t("account.platformAdmin")}
          </span>
        )}
      </div>

      <div className="mt-6 grid items-start gap-6 lg:grid-cols-2">
        <ProfileCard key={user.id} user={user} />
        <PasswordCard />
      </div>
    </div>
  );
}
