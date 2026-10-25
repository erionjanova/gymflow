import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { MailCheck, ShieldAlert } from "lucide-react";
import type { CSSProperties } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { toast } from "sonner";
import type { z } from "zod";
import { useInvitationPreview } from "@/api/queries";
import { FullPageSpinner } from "@/components/layout/RouteGuards";
import { Button, buttonClass } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { Field, Input } from "@/components/ui/Input";
import { currentLanguage, errorMessage } from "@/i18n";
import { api } from "@/lib/api";
import type { AcceptInvitationResponse } from "@/lib/types";
import { newAccountSchema } from "@/lib/validation";
import { useAuth } from "@/providers/AuthProvider";

type NewAccountForm = z.infer<typeof newAccountSchema>;

export default function InvitePage() {
  const { t } = useTranslation();
  const { token = "" } = useParams();
  const { user, startSession, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const { data: invite, isLoading, error } = useInvitationPreview(token);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<NewAccountForm>({ resolver: zodResolver(newAccountSchema) });

  const accept = useMutation({
    mutationFn: (body: Partial<NewAccountForm> & { preferredLanguage?: string }) =>
      api.post<AcceptInvitationResponse>(`/api/invitations/${encodeURIComponent(token)}/accept`, body),
    onSuccess: (result) => {
      if (result.auth) startSession(result.auth);
      queryClient.invalidateQueries();
      toast.success(t("invite.accepted", { name: result.gym.name }));
      navigate(`/g/${result.gym.slug}/admin`, { replace: true });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  if (isLoading) return <FullPageSpinner />;

  if (error || !invite) {
    return (
      <div className="mx-auto grid max-w-lg place-items-center gap-4 px-4 py-24 text-center">
        <ShieldAlert className="size-10 text-danger" />
        <h1 className="text-2xl font-semibold">{t("invite.invalidTitle")}</h1>
        <p className="text-muted">{errorMessage(error)}</p>
      </div>
    );
  }

  const closed = invite.status !== "Pending";
  const wrongAccount = user && user.email.toLowerCase() !== invite.email.toLowerCase();
  const role = t(`roles.${invite.role}`).toLowerCase();

  return (
    <div className="gym-theme mx-auto max-w-lg px-4 py-12 sm:py-20" style={{ "--brand": invite.brandColor } as CSSProperties}>
      <Card className="overflow-hidden">
        <div className="relative bg-accent px-8 py-10 text-accent-fg">
          <MailCheck className="size-9" />
          <h1 className="mt-4 text-3xl font-semibold">{t("invite.title", { gym: invite.gymName })}</h1>
          <p className="mt-2 opacity-85">{t("invite.subtitle", { role })}</p>
        </div>

        <div className="space-y-5 p-8">
          <p className="text-sm text-muted">
            {t("invite.for")} <span className="font-semibold text-fg">{invite.email}</span>
          </p>

          {closed ? (
            <div className="rounded-xl bg-surface-2 px-4 py-3 text-sm">{t(`invite.closed.${invite.status}`)}</div>
          ) : wrongAccount ? (
            <div className="space-y-3">
              <p className="text-sm">{t("invite.wrongAccount", { email: user.email })}</p>
              <Button variant="outline" onClick={logout}>
                {t("nav.logout")}
              </Button>
            </div>
          ) : user ? (
            <Button size="lg" className="w-full" onClick={() => accept.mutate({})} loading={accept.isPending}>
              {t("invite.accept")}
            </Button>
          ) : invite.hasAccount ? (
            <div className="space-y-3">
              <p className="text-sm text-muted">{t("invite.loginFirst")}</p>
              <Link to="/login" state={{ from: location.pathname }} className={buttonClass("primary", "lg", "w-full")}>
                {t("nav.login")}
              </Link>
            </div>
          ) : (
            <form
              onSubmit={handleSubmit((data) => accept.mutate({ ...data, preferredLanguage: currentLanguage() }))}
              className="space-y-5"
              noValidate
            >
              <p className="text-sm text-muted">{t("invite.createAccount")}</p>
              <Field label={t("auth.fullName")} error={errors.fullName?.message}>
                {(id, d) => <Input id={id} autoComplete="name" aria-describedby={d} aria-invalid={!!errors.fullName} {...register("fullName")} />}
              </Field>
              <Field label={t("auth.password")} error={errors.password?.message} hint={t("auth.passwordHint")}>
                {(id, d) => (
                  <Input id={id} type="password" autoComplete="new-password" aria-describedby={d} aria-invalid={!!errors.password} {...register("password")} />
                )}
              </Field>
              <Button type="submit" size="lg" className="w-full" loading={accept.isPending}>
                {t("invite.acceptAndCreate")}
              </Button>
            </form>
          )}
        </div>
      </Card>
    </div>
  );
}
