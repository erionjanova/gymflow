import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { BookingStatus, InvitationStatus, SubscriptionStatus, TenantRole } from "@/lib/types";
import { cn } from "@/lib/utils";

type Tone = "accent" | "success" | "warning" | "danger" | "neutral";

const tones: Record<Tone, string> = {
  accent: "bg-accent-soft text-accent",
  success: "bg-success-soft text-success",
  warning: "bg-warning-soft text-warning",
  danger: "bg-danger-soft text-danger",
  neutral: "bg-surface-2 text-muted",
};

export function Badge({ tone = "neutral", className, children }: { tone?: Tone; className?: string; children: ReactNode }) {
  return (
    <span className={cn("inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap", tones[tone], className)}>
      {children}
    </span>
  );
}

const bookingTones: Record<BookingStatus, Tone> = {
  Booked: "accent",
  Waitlisted: "warning",
  Cancelled: "neutral",
  Attended: "success",
  NoShow: "danger",
};

export function BookingBadge({ status, position }: { status: BookingStatus; position?: number | null }) {
  const { t } = useTranslation();
  return (
    <Badge tone={bookingTones[status]}>
      {status === "Waitlisted" && position ? t("booking.waitlistPosition", { position }) : t(`booking.status.${status}`)}
    </Badge>
  );
}

const subscriptionTones: Record<SubscriptionStatus, Tone> = {
  Incomplete: "neutral",
  Active: "success",
  PastDue: "warning",
  Canceled: "neutral",
};

export function SubscriptionBadge({ status }: { status: SubscriptionStatus }) {
  const { t } = useTranslation();
  return <Badge tone={subscriptionTones[status]}>{t(`subscription.status.${status}`)}</Badge>;
}

const invitationTones: Record<InvitationStatus, Tone> = {
  Pending: "warning",
  Accepted: "success",
  Revoked: "neutral",
  Expired: "neutral",
};

export function InvitationBadge({ status }: { status: InvitationStatus }) {
  const { t } = useTranslation();
  return <Badge tone={invitationTones[status]}>{t(`invite.status.${status}`)}</Badge>;
}

const roleTones: Record<TenantRole, Tone> = { Owner: "accent", Trainer: "success", Member: "neutral" };

export function RoleBadge({ role }: { role: TenantRole }) {
  const { t } = useTranslation();
  return <Badge tone={roleTones[role]}>{t(`roles.${role}`)}</Badge>;
}
