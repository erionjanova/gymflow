import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { MailPlus, RotateCw, Search, UserMinus, Users, X } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { z } from "zod";
import { keys, useInvitations, useMembers } from "@/api/queries";
import { InvitationBadge, RoleBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, Skeleton } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { Field, Input, Select } from "@/components/ui/Input";
import { ConfirmDialog, Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { api, ApiError, gymPath } from "@/lib/api";
import { formatDate } from "@/lib/format";
import type { Member, TenantRole } from "@/lib/types";
import { initials } from "@/lib/utils";
import { inviteSchema } from "@/lib/validation";
import { useAuth } from "@/providers/AuthProvider";
import { useGymContext } from "@/providers/GymProvider";

type InviteForm = z.infer<typeof inviteSchema>;

function InviteModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const { slug } = useGymContext();
  const queryClient = useQueryClient();

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<InviteForm>({ resolver: zodResolver(inviteSchema), defaultValues: { email: "", role: "Trainer" } });

  const onSubmit = async (data: InviteForm) => {
    try {
      await api.post(gymPath(slug, "/admin/invitations"), data);
      toast.success(t("invite.sent", { email: data.email }));
      queryClient.invalidateQueries({ queryKey: keys.invitations(slug) });
      reset();
      onClose();
    } catch (e) {
      if (e instanceof ApiError && e.code === "already_staff") setError("email", { message: errorMessage(e) });
      else toast.error(errorMessage(e));
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={t("invite.new")}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button onClick={handleSubmit(onSubmit)} loading={isSubmitting}>
            {t("invite.send")}
          </Button>
        </>
      }
    >
      <form className="space-y-5" onSubmit={handleSubmit(onSubmit)} noValidate>
        <p className="text-sm text-muted">{t("invite.newHint")}</p>
        <Field label={t("auth.email")} error={errors.email?.message}>
          {(id, d) => <Input id={id} type="email" aria-describedby={d} aria-invalid={!!errors.email} {...register("email")} />}
        </Field>
        <Field label={t("invite.role")}>
          {(id) => (
            <Select id={id} {...register("role")}>
              <option value="Trainer">{t("roles.Trainer")}</option>
              <option value="Owner">{t("roles.Owner")}</option>
            </Select>
          )}
        </Field>
      </form>
    </Modal>
  );
}

function Invitations() {
  const { t, i18n } = useTranslation();
  const { slug } = useGymContext();
  const queryClient = useQueryClient();
  const { data: invitations } = useInvitations(slug);

  const action = useMutation({
    mutationFn: ({ id, kind }: { id: string; kind: "resend" | "revoke" }) =>
      kind === "resend" ? api.post(gymPath(slug, `/admin/invitations/${id}/resend`)) : api.delete(gymPath(slug, `/admin/invitations/${id}`)),
    onSuccess: (_, v) => {
      toast(v.kind === "resend" ? t("invite.resent") : t("invite.revoked"));
      queryClient.invalidateQueries({ queryKey: keys.invitations(slug) });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const visible = (invitations ?? []).filter((i) => i.status !== "Revoked").slice(0, 10);
  if (!visible.length) return null;

  return (
    <Card className="p-6">
      <h2 className="mb-4 text-lg font-semibold">{t("invite.listTitle")}</h2>
      <ul className="divide-y divide-line">
        {visible.map((inv) => (
          <li key={inv.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
            <div className="min-w-0">
              <p className="truncate font-medium">{inv.email}</p>
              <p className="text-xs text-muted">
                {t(`roles.${inv.role}`)} · {t("invite.sentBy", { name: inv.invitedBy, date: formatDate(inv.createdAt, i18n.language) })}
              </p>
            </div>
            <div className="flex items-center gap-1">
              <InvitationBadge status={inv.status} />
              {(inv.status === "Pending" || inv.status === "Expired") && (
                <Button variant="ghost" size="icon" aria-label={t("invite.resend")} title={t("invite.resend")} onClick={() => action.mutate({ id: inv.id, kind: "resend" })}>
                  <RotateCw className="size-4" />
                </Button>
              )}
              {inv.status === "Pending" && (
                <Button variant="ghost" size="icon" aria-label={t("invite.revoke")} title={t("invite.revoke")} onClick={() => action.mutate({ id: inv.id, kind: "revoke" })}>
                  <X className="size-4" />
                </Button>
              )}
            </div>
          </li>
        ))}
      </ul>
    </Card>
  );
}

export default function MembersPage() {
  const { t, i18n } = useTranslation();
  const { slug, isOwner } = useGymContext();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const { data: members, isLoading } = useMembers(slug);
  const [search, setSearch] = useState("");
  const [inviteOpen, setInviteOpen] = useState(false);
  const [removing, setRemoving] = useState<Member | null>(null);

  const changeRole = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: TenantRole }) => api.put(gymPath(slug, `/admin/members/${userId}/role`), { role }),
    onSuccess: () => {
      toast.success(t("common.saved"));
      queryClient.invalidateQueries({ queryKey: keys.members(slug) });
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const remove = useMutation({
    mutationFn: (userId: string) => api.delete(gymPath(slug, `/admin/members/${userId}`)),
    onSuccess: () => {
      toast(t("admin.members.removed"));
      queryClient.invalidateQueries({ queryKey: keys.members(slug) });
      setRemoving(null);
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const term = search.trim().toLowerCase();
  const filtered = (members ?? []).filter((m) => !term || m.fullName.toLowerCase().includes(term) || m.email.toLowerCase().includes(term));

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("admin.members.title")}
        subtitle={t("admin.members.subtitle", { count: members?.length ?? 0 })}
        actions={
          isOwner && (
            <Button onClick={() => setInviteOpen(true)}>
              <MailPlus className="size-4" />
              {t("invite.new")}
            </Button>
          )
        }
      />

      {isOwner && <Invitations />}

      <div className="relative max-w-sm">
        <Search className="pointer-events-none absolute top-1/2 left-4 size-4 -translate-y-1/2 text-muted" />
        <Input className="pl-11" placeholder={t("admin.members.search")} value={search} onChange={(e) => setSearch(e.target.value)} aria-label={t("admin.members.search")} />
      </div>

      {isLoading ? (
        <Skeleton className="h-48" />
      ) : !filtered.length ? (
        <EmptyState icon={Users} text={t("admin.members.empty")} />
      ) : (
        <Card className="overflow-hidden">
          <ul className="divide-y divide-line">
            {filtered.map((m) => {
              const isMe = m.userId === user?.id;
              return (
                <li key={m.userId} className="flex flex-wrap items-center gap-4 px-5 py-3">
                  <span className="grid size-10 shrink-0 place-items-center rounded-full bg-accent-soft text-xs font-bold text-accent">{initials(m.fullName)}</span>
                  <div className="min-w-0 flex-1 basis-48">
                    <p className="truncate font-medium">
                      {m.fullName}
                      {isMe && <span className="ml-1.5 text-xs text-muted">({t("admin.members.you")})</span>}
                    </p>
                    <p className="truncate text-xs text-muted">
                      {m.email} · {t("admin.members.joined", { date: formatDate(m.joinedAt, i18n.language) })}
                    </p>
                  </div>
                  {isOwner && !isMe ? (
                    <div className="ml-auto flex items-center gap-1">
                      <Select
                        aria-label={t("invite.role")}
                        className="h-9 w-36 rounded-full text-sm"
                        value={m.role}
                        onChange={(e) => changeRole.mutate({ userId: m.userId, role: e.target.value as TenantRole })}
                      >
                        {(["Member", "Trainer", "Owner"] as const).map((r) => (
                          <option key={r} value={r}>
                            {t(`roles.${r}`)}
                          </option>
                        ))}
                      </Select>
                      <Button variant="ghost" size="icon" aria-label={t("admin.members.remove")} title={t("admin.members.remove")} onClick={() => setRemoving(m)}>
                        <UserMinus className="size-4" />
                      </Button>
                    </div>
                  ) : (
                    <span className="ml-auto">
                      <RoleBadge role={m.role} />
                    </span>
                  )}
                </li>
              );
            })}
          </ul>
        </Card>
      )}

      <InviteModal open={inviteOpen} onClose={() => setInviteOpen(false)} />
      <ConfirmDialog
        open={!!removing}
        title={t("admin.members.removeTitle")}
        description={t("admin.members.removeText", { name: removing?.fullName })}
        confirmLabel={t("admin.members.remove")}
        destructive
        loading={remove.isPending}
        onConfirm={() => removing && remove.mutate(removing.userId)}
        onClose={() => setRemoving(null)}
      />
    </div>
  );
}
