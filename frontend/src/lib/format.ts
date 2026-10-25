import type { DayOfWeek, Language } from "./types";

const LOCALES: Record<Language, string> = { sq: "sq-AL", en: "en-GB" };

export const localeOf = (lang: string) => (lang?.toLowerCase().startsWith("en") ? LOCALES.en : LOCALES.sq);

const SQ_NAMES = {
  monthLong: ["janar", "shkurt", "mars", "prill", "maj", "qershor", "korrik", "gusht", "shtator", "tetor", "nëntor", "dhjetor"],
  monthShort: ["jan", "shk", "mar", "pri", "maj", "qer", "korr", "gush", "sht", "tet", "nën", "dhj"],
  weekdayLong: ["e diel", "e hënë", "e martë", "e mërkurë", "e enjte", "e premte", "e shtunë"],
  weekdayShort: ["die", "hën", "mar", "mër", "enj", "pre", "sht"],
};

const hasNativeAlbanian = Intl.DateTimeFormat.supportedLocalesOf(["sq"]).length > 0;

function dateFormat(lang: string, options: Intl.DateTimeFormatOptions): { format: (date: Date) => string } {
  const locale = localeOf(lang);
  if (locale !== LOCALES.sq || hasNativeAlbanian) return new Intl.DateTimeFormat(locale, options);

  const base = new Intl.DateTimeFormat("en-GB", options);
  const index = new Intl.DateTimeFormat("en-US", { timeZone: options.timeZone, month: "numeric", weekday: "short" });
  const weekdays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

  return {
    format: (date) => {
      const idx = index.formatToParts(date);
      const month = Number(idx.find((p) => p.type === "month")!.value) - 1;
      const weekday = weekdays.indexOf(idx.find((p) => p.type === "weekday")!.value);
      const parts = base.formatToParts(date);
      return parts
        .map((p, i) => {
          if (p.type === "month" && Number.isNaN(Number(p.value)))
            return options.month === "long" ? SQ_NAMES.monthLong[month] : SQ_NAMES.monthShort[month];
          if (p.type === "weekday") return options.weekday === "long" ? SQ_NAMES.weekdayLong[weekday] : SQ_NAMES.weekdayShort[weekday];
          if (p.type === "literal" && parts[i - 1]?.type === "weekday" && p.value === " ") return ", ";
          return p.value;
        })
        .join("");
    },
  };
}

export function formatDateTime(iso: string, lang: string, timeZone: string, options: Intl.DateTimeFormatOptions) {
  return dateFormat(lang, { timeZone, ...options }).format(new Date(iso));
}

export const formatTime = (iso: string, lang: string, timeZone: string) =>
  formatDateTime(iso, lang, timeZone, { hour: "2-digit", minute: "2-digit", hour12: false });

export const formatDayHeading = (iso: string, lang: string, timeZone: string) =>
  formatDateTime(iso, lang, timeZone, { weekday: "long", day: "numeric", month: "long" });

export const formatShortDate = (iso: string, lang: string, timeZone: string) =>
  formatDateTime(iso, lang, timeZone, { weekday: "short", day: "numeric", month: "short" });

export const formatDate = (iso: string, lang: string) =>
  dateFormat(lang, { day: "numeric", month: "short", year: "numeric" }).format(new Date(iso));

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
  dateFormat(lang, { timeZone: "UTC", ...options }).format(parseDate(date));

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
