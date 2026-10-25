import { useTranslation } from "react-i18next";
import { currentLanguage, LANGUAGES } from "@/i18n";
import { api } from "@/lib/api";
import type { Language, User } from "@/lib/types";
import { cn } from "@/lib/utils";
import { useAuth } from "@/providers/AuthProvider";

export function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const { user, setUser } = useAuth();
  const active = currentLanguage();

  const change = async (lang: Language) => {
    if (lang === active) return;
    await i18n.changeLanguage(lang);
    if (user && user.preferredLanguage !== lang) {
      api
        .put<User>("/api/auth/me", { fullName: user.fullName, phoneNumber: user.phoneNumber, preferredLanguage: lang })
        .then(setUser)
        .catch(() => {});
    }
  };

  return (
    <div role="group" aria-label={t("nav.language")} className="flex h-10 items-center rounded-full border border-line bg-surface p-1">
      {LANGUAGES.map((l) => (
        <button
          key={l.code}
          onClick={() => change(l.code)}
          aria-pressed={active === l.code}
          title={l.label}
          className={cn(
            "h-full cursor-pointer rounded-full px-3 text-xs font-semibold transition-colors",
            active === l.code ? "bg-fg text-bg" : "text-muted hover:text-fg",
          )}
        >
          {l.short}
        </button>
      ))}
    </div>
  );
}
