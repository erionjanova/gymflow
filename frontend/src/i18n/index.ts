import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";
import { ApiError } from "@/lib/api";
import en from "./locales/en";
import sq from "./locales/sq";

export const LANGUAGES = [
  { code: "sq", label: "Shqip", short: "SQ" },
  { code: "en", label: "English", short: "EN" },
] as const;

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: { sq: { translation: sq }, en: { translation: en } },
    fallbackLng: "sq",
    supportedLngs: ["sq", "en"],
    nonExplicitSupportedLngs: true,
    interpolation: { escapeValue: false },
    detection: {
      order: ["localStorage", "navigator"],
      lookupLocalStorage: "lang",
      caches: ["localStorage"],
    },
  });

const syncHtmlLang = (lng: string) => document.documentElement.setAttribute("lang", lng.slice(0, 2));
syncHtmlLang(i18n.resolvedLanguage ?? "sq");
i18n.on("languageChanged", syncHtmlLang);

export const currentLanguage = () => ((i18n.resolvedLanguage ?? "sq").startsWith("en") ? "en" : "sq");

export const errorMessage = (error: unknown) => {
  const code = error instanceof ApiError ? error.code : "server_error";
  return i18n.t(`errors.${code}` as "errors.server_error", { defaultValue: i18n.t("errors.server_error") });
};

export default i18n;
