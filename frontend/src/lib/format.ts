import type { DayOfWeek, Language } from "./types";

const LOCALES: Record<Language, string> = { sq: "sq-AL", en: "en-GB" };

export const localeOf = (lang: string) => LOCALES[(lang as Language) in LOCALES ? (lang as Language) : "sq"];

export function formatDateTime(iso: string, lang: string, timeZone: string, options: Intl.DateTimeFormatOptions) {
  return new Intl.DateTimeFormat(localeOf(lang), { timeZone, ...options }).format(new Date(iso));
}

export const formatTime = (iso: string, lang: string, timeZone: string) =>
  formatDateTime(iso, lang, timeZone, { hour: "2-digit", minute: "2-digit", hour12: false });

export const formatDayHeading = (iso: string, lang: string, timeZone: string) =>
  formatDateTime(iso, lang, timeZone, { weekday: "long", day: "numeric", month: "long" });

export const formatShortDate = (iso: string, lang: string, timeZone: string) =>
  formatDateTime(iso, lang, timeZone, { weekday: "short", day: "numeric", month: "short" });

export const formatDate = (iso: string, lang: string) =>
  new Intl.DateTimeFormat(localeOf(lang), { day: "numeric", month: "short", year: "numeric" }).format(new Date(iso));

export const localDateOf = (iso: string, timeZone: string) =>
  new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date(iso));

export const todayIn = (timeZone: string) => localDateOf(new Date().toISOString(), timeZone);

const parseDate = (date: string) => {
  const [y, m, d] = date.split("-").map(Number);
  return new Date(Date.UTC(y!, m! - 1, d!));
};

export const addDays = (date: string, days: number) => {
  const d = parseDate(date);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

export const formatCalendarDate = (date: string, lang: string, options: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat(localeOf(lang), { timeZone: "UTC", ...options }).format(parseDate(date));

export const zonedDateTimeToIso = (date: string, time: string, timeZone: string) => {
  const [y, m, d] = date.split("-").map(Number);
  const [hh, mm] = time.split(":").map(Number);
  const guess = Date.UTC(y!, m! - 1, d!, hh!, mm!);
  const offsetAt = (instant: number) => {
    const parts = new Intl.DateTimeFormat("en-US", {
      timeZone,
      hourCycle: "h23",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    }).formatToParts(new Date(instant));
    const get = (type: string) => Number(parts.find((p) => p.type === type)!.value);
    return Date.UTC(get("year"), get("month") - 1, get("day"), get("hour"), get("minute")) - instant;
  };
  const first = guess - offsetAt(guess);
  return new Date(guess - offsetAt(first)).toISOString();
};

export const zonedRangeOfDays = (fromDate: string, days: number, timeZone: string) => ({
  from: zonedDateTimeToIso(fromDate, "00:00", timeZone),
  to: zonedDateTimeToIso(addDays(fromDate, days), "00:00", timeZone),
});

export const formatPrice = (price: number, currency: string, lang: string) =>
  new Intl.NumberFormat(localeOf(lang), {
    style: "currency",
    currency,
    minimumFractionDigits: price % 1 === 0 ? 0 : 2,
  }).format(price);

export const WEEK_DAYS: DayOfWeek[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
