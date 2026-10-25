import { CalendarCheck, CreditCard, Dumbbell } from "lucide-react";
import { motion } from "motion/react";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

export function AuthLayout({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  const { t } = useTranslation();

  return (
    <div className="mx-auto grid max-w-[100rem] gap-10 px-4 py-10 sm:px-6 md:py-16 lg:grid-cols-2 lg:gap-16 lg:px-10 2xl:px-16">
      <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} className="mx-auto w-full max-w-md lg:py-8">
        <h1 className="text-4xl font-semibold">{title}</h1>
        <p className="mt-2 text-muted">{subtitle}</p>
        <div className="mt-8">{children}</div>
      </motion.div>

      <motion.aside
        initial={{ opacity: 0, scale: 0.98 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ delay: 0.1 }}
        className="relative hidden overflow-hidden rounded-3xl bg-fg p-10 text-bg lg:flex lg:flex-col lg:justify-between"
      >
        <div className="hero-glow pointer-events-none absolute inset-0" />
        <div className="relative grid size-12 place-items-center rounded-2xl bg-accent text-accent-fg">
          <Dumbbell className="size-6" />
        </div>
        <div className="relative">
          <h2 className="text-4xl leading-tight font-semibold">{t("auth.sideTitle")}</h2>
          <p className="mt-4 max-w-sm opacity-70">{t("auth.sideText")}</p>
          <div className="mt-8 flex flex-col gap-3 text-sm">
            <span className="flex items-center gap-3 opacity-80">
              <CalendarCheck className="size-4 text-accent" /> {t("landing.features.classes.title")}
            </span>
            <span className="flex items-center gap-3 opacity-80">
              <CreditCard className="size-4 text-accent" /> {t("landing.features.payments.title")}
            </span>
          </div>
        </div>
      </motion.aside>
    </div>
  );
}
