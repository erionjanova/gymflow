import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { buttonClass } from "@/components/ui/Button";

export default function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <div className="mx-auto grid max-w-lg place-items-center gap-4 px-4 py-24 text-center">
      <p className="font-display text-7xl font-bold text-accent">404</p>
      <h1 className="text-2xl font-semibold">{t("notFound.title")}</h1>
      <p className="text-muted">{t("notFound.text")}</p>
      <Link to="/" className={buttonClass("outline")}>
        {t("notFound.home")}
      </Link>
    </div>
  );
}
