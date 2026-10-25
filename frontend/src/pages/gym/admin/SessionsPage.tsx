import { useMutation, useQueryClient } from "@tanstack/react-query";
import { CalendarPlus, CalendarX, Check, ChevronLeft, ChevronRight, Pencil, Repeat, Users, X } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { keys, useClasses, useMembers, useRoster, useStaffSessions } from "@/api/queries";
import { Badge, BookingBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { Field, Input, Select } from "@/components/ui/Input";
import { ConfirmDialog, Modal } from "@/components/ui/Modal";
import { PageHeader } from "@/components/ui/PageHeader";
import { errorMessage } from "@/i18n";
import { api, gymPath } from "@/lib/api";
import {
  addDays,
  formatCalendarDate,
  formatDayHeading,
  formatTime,
  localDateOf,
  startOfWeek,
  todayIn,
  WEEK_DAYS,
  zonedDateTimeToIso,
  zonedRangeOfDays,
} from "@/lib/format";
import type { DayOfWeek, ScheduleResult, Session } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useGymContext } from "@/providers/GymProvider";
import { groupByDay } from "../SchedulePage";

function useStaffOptions() {
  const { slug } = useGymContext();
  const { data: members } = useMembers(slug);
  return (members ?? []).filter((m) => m.role !== "Member");
}

function RosterModal({ session, onClose }: { session: Session | null; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const { gym, slug } = useGymContext();
  const queryClient = useQueryClient();
  const { data: roster, isLoading } = useRoster(slug, session?.id ?? null);

  const mark = useMutation({
    mutationFn: ({ userId, attended }: { userId: string; attended: boolean }) =>
      api.put(gymPath(slug, `/admin/sessions/${session!.id}/attendance/${userId}`), { attended }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.roster(slug, session!.id) }),
    onError: (e) => toast.error(errorMessage(e)),
  });

  const attendanceOpen = session && new Date(session.startsAt).getTime() - Date.now() <= 30 * 60_000;

  return (
    <Modal
      open={!!session}
      onClose={onClose}
      title={session ? `${session.className} · ${formatTime(session.startsAt, i18n.language, gym.timeZone)}` : ""}
    >
      {isLoading ? (
        <Skeleton className="h-32" />
      ) : !roster?.length ? (
        <p className="text-sm text-muted">{t("admin.sessions.rosterEmpty")}</p>
      ) : (
        <>
          {!attendanceOpen && <p className="mb-4 rounded-xl bg-surface-2 px-4 py-3 text-xs text-muted">{t("admin.sessions.attendanceLater")}</p>}
          <ul className="divide-y divide-line">
            {roster.map((r) => (
              <li key={r.bookingId} className="flex items-center justify-between gap-3 py-3">
                <div className="min-w-0">
                  <p className="truncate font-medium">{r.fullName}</p>
                  <p className="truncate text-xs text-muted">{r.email}</p>
                </div>
                <div className="flex items-center gap-2">
                  <BookingBadge status={r.status} position={r.waitlistPosition} />
                  {attendanceOpen && r.status !== "Waitlisted" && (
                    <>
                      <Button
                        size="icon"
                        variant={r.status === "Attended" ? "primary" : "outline"}
                        aria-label={t("admin.sessions.markAttended")}
                        title={t("admin.sessions.markAttended")}
                        className="size-9"
                        onClick={() => mark.mutate({ userId: r.userId, attended: true })}
                      >
                        <Check className="size-4" />
                      </Button>
                      <Button
                        size="icon"
                        variant={r.status === "NoShow" ? "danger" : "outline"}
                        aria-label={t("admin.sessions.markNoShow")}
                        title={t("admin.sessions.markNoShow")}
                        className="size-9"
                        onClick={() => mark.mutate({ userId: r.userId, attended: false })}
                      >
                        <X className="size-4" />
                      </Button>
                    </>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </Modal>
  );
}

function SessionFormModal({
  open,
  onClose,
  session,
}: {
  open: boolean;
  onClose: () => void;
  session: Session | null;
}) {
  const { t } = useTranslation();
  const { gym, slug } = useGymContext();
  const queryClient = useQueryClient();
  const { data: classes } = useClasses(slug, true);
  const staff = useStaffOptions();
  const editing = !!session;

  const [mode, setMode] = useState<"single" | "weekly">("single");
  const [classId, setClassId] = useState("");
  const [trainerId, setTrainerId] = useState("");
  const [date, setDate] = useState(addDays(todayIn(gym.timeZone), 1));
  const [toDate, setToDate] = useState(addDays(todayIn(gym.timeZone), 28));
  const [time, setTime] = useState("18:00");
  const [capacity, setCapacity] = useState("");
  const [days, setDays] = useState<DayOfWeek[]>(["Monday", "Wednesday"]);

  const [initialized, setInitialized] = useState<string | null>(null);
  const key = open ? (session?.id ?? "new") : null;
  if (key !== initialized) {
    setInitialized(key);
    if (session) {
      setClassId(session.classId);
      setTrainerId(session.trainerId ?? "");
      setDate(localDateOf(session.startsAt, gym.timeZone));
      setTime(formatTime(session.startsAt, "en", gym.timeZone));
      setCapacity(String(session.capacity));
    } else if (open) {
      setMode("single");
      setClassId("");
      setTrainerId("");
      setCapacity("");
    }
  }

  const activeClasses = (classes ?? []).filter((c) => c.isActive || c.id === classId);
  const selectedClass = activeClasses.find((c) => c.id === classId);

  const save = useMutation({
    mutationFn: async () => {
      const cap = capacity ? Number(capacity) : null;
      const trainer = trainerId || null;
      if (editing) {
        return api.put<Session>(gymPath(slug, `/admin/sessions/${session.id}`), {
          trainerId: trainer,
          startsAt: zonedDateTimeToIso(date, time, gym.timeZone),
          capacity: cap ?? session.capacity,
        });
      }
      if (mode === "weekly") {
        const result = await api.post<ScheduleResult>(gymPath(slug, "/admin/sessions/schedule"), {
          gymClassId: classId,
          trainerId: trainer,
          fromDate: date,
          toDate,
          daysOfWeek: days,
          startTime: `${time}:00`,
          capacity: cap,
        });
        toast.success(t("admin.sessions.scheduled", { count: result.created }));
        return result;
      }
      return api.post<Session>(gymPath(slug, "/admin/sessions"), {
        gymClassId: classId,
        trainerId: trainer,
        startsAt: zonedDateTimeToIso(date, time, gym.timeZone),
        capacity: cap,
      });
    },
    onSuccess: () => {
      if (editing || mode === "single") toast.success(t("common.saved"));
      queryClient.invalidateQueries({ queryKey: ["gym", slug] });
      onClose();
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const canSave = !!classId && !!date && !!time && (mode === "single" || editing || days.length > 0);

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={editing ? t("admin.sessions.edit") : t("admin.sessions.new")}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!canSave}>
            {t("common.save")}
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        {!editing && (
          <div className="grid grid-cols-2 gap-2">
            {(["single", "weekly"] as const).map((m) => (
              <button
                key={m}
                type="button"
                onClick={() => setMode(m)}
                className={cn(
                  "flex cursor-pointer items-center justify-center gap-2 rounded-xl border px-3 py-3 text-sm font-medium transition-colors",
                  mode === m ? "border-accent bg-accent-soft text-accent" : "border-line text-muted hover:text-fg",
                )}
              >
                {m === "single" ? <CalendarPlus className="size-4" /> : <Repeat className="size-4" />}
                {t(`admin.sessions.mode.${m}`)}
              </button>
            ))}
          </div>
        )}

        <Field label={t("admin.sessions.class")}>
          {(id) => (
            <Select id={id} value={classId} disabled={editing} onChange={(e) => setClassId(e.target.value)}>
              <option value="">{t("admin.sessions.pickClass")}</option>
              {activeClasses.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          )}
        </Field>

        <Field label={t("admin.sessions.trainer")} optional>
          {(id) => (
            <Select id={id} value={trainerId} onChange={(e) => setTrainerId(e.target.value)}>
              <option value="">{t("admin.sessions.noTrainer")}</option>
              {staff.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.fullName}
                </option>
              ))}
            </Select>
          )}
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={mode === "weekly" && !editing ? t("admin.sessions.fromDate") : t("admin.sessions.date")}>
            {(id) => <Input id={id} type="date" value={date} onChange={(e) => setDate(e.target.value)} />}
          </Field>
          {mode === "weekly" && !editing ? (
            <Field label={t("admin.sessions.toDate")}>
              {(id) => <Input id={id} type="date" value={toDate} min={date} onChange={(e) => setToDate(e.target.value)} />}
            </Field>
          ) : (
            <Field label={t("admin.sessions.time")}>
              {(id) => <Input id={id} type="time" value={time} onChange={(e) => setTime(e.target.value)} />}
            </Field>
          )}
        </div>

        {mode === "weekly" && !editing && (
          <>
            <Field label={t("admin.sessions.time")}>
              {(id) => <Input id={id} type="time" value={time} onChange={(e) => setTime(e.target.value)} />}
            </Field>
            <div>
              <p className="mb-2 text-sm font-medium">{t("admin.sessions.days")}</p>
              <div className="flex flex-wrap gap-2">
                {WEEK_DAYS.map((d) => {
                  const on = days.includes(d);
                  return (
                    <button
                      key={d}
                      type="button"
                      aria-pressed={on}
                      onClick={() => setDays(on ? days.filter((x) => x !== d) : [...days, d])}
                      className={cn(
                        "h-10 min-w-12 cursor-pointer rounded-full border px-3 text-sm font-semibold transition-colors",
                        on ? "border-accent bg-accent text-accent-fg" : "border-line text-muted hover:text-fg",
                      )}
                    >
                      {t(`days.short.${d}`)}
                    </button>
                  );
                })}
              </div>
            </div>
          </>
        )}

        <Field
          label={t("admin.sessions.capacity")}
          optional={!editing}
          hint={selectedClass && !capacity ? t("admin.sessions.capacityDefault", { count: selectedClass.defaultCapacity }) : undefined}
        >
          {(id, d) => (
            <Input id={id} type="number" min={1} max={500} value={capacity} aria-describedby={d} onChange={(e) => setCapacity(e.target.value)} />
          )}
        </Field>
        <p className="text-xs text-muted">{t("admin.sessions.timeZoneNote", { zone: gym.timeZone })}</p>
      </div>
    </Modal>
  );
}

export default function SessionsPage() {
  const { t, i18n } = useTranslation();
  const { gym, slug, isOwner } = useGymContext();
  const queryClient = useQueryClient();
  const today = todayIn(gym.timeZone);
  const [weekStart, setWeekStart] = useState(startOfWeek(today));
  const [rosterFor, setRosterFor] = useState<Session | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Session | null>(null);
  const [cancelling, setCancelling] = useState<Session | null>(null);

  const range = useMemo(() => zonedRangeOfDays(weekStart, 7, gym.timeZone), [weekStart, gym.timeZone]);
  const { data: sessions, isLoading } = useStaffSessions(slug, range.from, range.to);
  const days = groupByDay(sessions ?? [], gym.timeZone);

  const cancel = useMutation({
    mutationFn: (id: string) => api.post(gymPath(slug, `/admin/sessions/${id}/cancel`)),
    onSuccess: () => {
      toast(t("admin.sessions.cancelled"));
      queryClient.invalidateQueries({ queryKey: ["gym", slug] });
      setCancelling(null);
    },
    onError: (e) => toast.error(errorMessage(e)),
  });

  const weekLabel = `${formatCalendarDate(weekStart, i18n.language, { day: "numeric", month: "short" })} – ${formatCalendarDate(addDays(weekStart, 6), i18n.language, { day: "numeric", month: "short" })}`;

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("admin.sessions.title")}
        subtitle={isOwner ? t("admin.sessions.subtitleOwner") : t("admin.sessions.subtitleTrainer")}
        actions={
          isOwner && (
            <Button
              onClick={() => {
                setEditing(null);
                setFormOpen(true);
              }}
            >
              <CalendarPlus className="size-4" />
              {t("admin.sessions.new")}
            </Button>
          )
        }
      />

      <div className="flex items-center gap-2">
        <Button variant="outline" size="icon" aria-label={t("schedule.prevWeek")} onClick={() => setWeekStart(addDays(weekStart, -7))}>
          <ChevronLeft className="size-4" />
        </Button>
        <span className="min-w-36 text-center text-sm font-semibold">{weekLabel}</span>
        <Button variant="outline" size="icon" aria-label={t("schedule.nextWeek")} onClick={() => setWeekStart(addDays(weekStart, 7))}>
          <ChevronRight className="size-4" />
        </Button>
        {weekStart !== startOfWeek(today) && (
          <Button variant="ghost" size="sm" onClick={() => setWeekStart(startOfWeek(today))}>
            {t("schedule.today")}
          </Button>
        )}
      </div>

      {isLoading ? (
        <Skeleton className="h-48" />
      ) : days.length === 0 ? (
        <EmptyState icon={CalendarX} text={t("admin.sessions.empty")} />
      ) : (
        days.map(([day, list]) => (
          <section key={day}>
            <h2 className={cn("mb-2 text-sm font-semibold tracking-wide uppercase", day === today ? "text-accent" : "text-muted")}>
              {formatDayHeading(list[0]!.startsAt, i18n.language, gym.timeZone)}
            </h2>
            <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
              {list.map((s) => {
                const past = new Date(s.startsAt).getTime() <= Date.now();
                return (
                  <li key={s.id} className={cn("flex flex-wrap items-center gap-4 px-4 py-3 sm:px-5", s.isCancelled && "opacity-60")}>
                    <span className="w-14 font-display text-lg font-semibold tabular-nums">{formatTime(s.startsAt, i18n.language, gym.timeZone)}</span>
                    <div className="min-w-0 flex-1">
                      <p className="font-medium">
                        {s.className}
                        {s.isCancelled && (
                          <Badge tone="danger" className="ml-2">
                            {t("admin.sessions.cancelledBadge")}
                          </Badge>
                        )}
                      </p>
                      <p className="text-xs text-muted">{s.trainerName ?? t("admin.sessions.noTrainer")}</p>
                    </div>
                    <span className="text-sm font-semibold tabular-nums">
                      {s.bookedCount}/{s.capacity}
                      {s.waitlistCount > 0 && <span className="ml-1 text-xs text-warning">+{s.waitlistCount}</span>}
                    </span>
                    <div className="flex items-center gap-1">
                      <Button variant="ghost" size="sm" onClick={() => setRosterFor(s)} disabled={s.isCancelled}>
                        <Users className="size-4" />
                        {t("admin.sessions.roster")}
                      </Button>
                      {isOwner && !past && !s.isCancelled && (
                        <>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label={t("common.edit")}
                            onClick={() => {
                              setEditing(s);
                              setFormOpen(true);
                            }}
                          >
                            <Pencil className="size-4" />
                          </Button>
                          <Button variant="ghost" size="icon" aria-label={t("admin.sessions.cancelSession")} onClick={() => setCancelling(s)}>
                            <X className="size-4" />
                          </Button>
                        </>
                      )}
                    </div>
                  </li>
                );
              })}
            </ul>
          </section>
        ))
      )}

      <RosterModal session={rosterFor} onClose={() => setRosterFor(null)} />
      <SessionFormModal open={formOpen} session={editing} onClose={() => setFormOpen(false)} />
      <ConfirmDialog
        open={!!cancelling}
        title={t("admin.sessions.cancelTitle")}
        description={t("admin.sessions.cancelText", { count: (cancelling?.bookedCount ?? 0) + (cancelling?.waitlistCount ?? 0) })}
        confirmLabel={t("admin.sessions.cancelSession")}
        destructive
        loading={cancel.isPending}
        onConfirm={() => cancelling && cancel.mutate(cancelling.id)}
        onClose={() => setCancelling(null)}
      />
    </div>
  );
}
