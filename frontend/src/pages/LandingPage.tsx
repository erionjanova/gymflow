import { ArrowRight, BarChart3, CalendarCheck, CreditCard, ShieldCheck, Users } from "lucide-react";
import { motion } from "motion/react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { buttonClass } from "@/components/ui/Button";
import { useAuth } from "@/providers/AuthProvider";

const features = [
  { key: "classes", icon: CalendarCheck },
  { key: "payments", icon: CreditCard },
  { key: "team", icon: Users },
  { key: "insights", icon: BarChart3 },
] as const;

export default function LandingPage() {
  const { t } = useTranslation();
  const { user } = useAuth();

  return (
    <>
      <section className="relative overflow-hidden">
        <div className="hero-glow pointer-events-none absolute inset-0" />
        <div className="relative mx-auto grid max-w-[100rem] gap-12 px-4 pt-16 pb-20 sm:px-6 md:pt-24 lg:grid-cols-[1.1fr_0.9fr] lg:items-center lg:px-10 2xl:px-16">
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
            <span className="inline-flex items-center gap-2 rounded-full border border-line bg-surface px-3 py-1 text-xs font-semibold text-muted">
              <ShieldCheck className="size-3.5 text-accent" />
              {t("landing.badge")}
            </span>
            <h1 className="mt-6 text-5xl leading-[1.05] font-bold sm:text-6xl lg:text-7xl">
              {t("landing.titleStart")} <span className="text-accent">{t("landing.titleAccent")}</span>
            </h1>
            <p className="mt-6 max-w-xl text-lg text-muted">{t("landing.subtitle")}</p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link to={user ? "/gyms/new" : "/register"} state={user ? undefined : { from: "/gyms/new" }} className={buttonClass("primary", "lg")}>
                {t("landing.ctaCreate")}
                <ArrowRight className="size-4" />
              </Link>
              <Link to="/g/powergym" className={buttonClass("outline", "lg")}>
                {t("landing.ctaDemo")}
              </Link>
            </div>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, scale: 0.96 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ delay: 0.1 }}
            className="rounded-3xl border border-line bg-surface p-5 shadow-xl shadow-black/5"
          >
            <div className="flex items-center justify-between">
              <p className="font-display text-lg font-semibold">{t("landing.preview.title")}</p>
              <span className="rounded-full bg-accent-soft px-2.5 py-0.5 text-xs font-semibold text-accent">{t("landing.preview.live")}</span>
            </div>
            <ul className="mt-4 space-y-2.5">
              {[
                { time: "07:00", name: "Spin", spots: 4, capacity: 20 },
                { time: "18:00", name: "Yoga Flow", spots: 0, capacity: 16 },
                { time: "19:00", name: "HIIT", spots: 7, capacity: 12 },
              ].map((s) => (
                <li key={s.name} className="flex items-center gap-4 rounded-2xl bg-surface-2 px-4 py-3">
                  <span className="font-display text-lg font-semibold tabular-nums">{s.time}</span>
                  <div className="min-w-0 flex-1">
                    <p className="font-medium">{s.name}</p>
                    <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-line">
                      <div className="h-full rounded-full bg-accent" style={{ width: `${((s.capacity - s.spots) / s.capacity) * 100}%` }} />
                    </div>
                  </div>
                  <span className="text-xs font-semibold text-muted">
                    {s.spots === 0 ? t("landing.preview.waitlist") : t("landing.preview.spots", { count: s.spots })}
                  </span>
                </li>
              ))}
            </ul>
          </motion.div>
        </div>
      </section>

      <section className="mx-auto max-w-[100rem] px-4 pb-24 sm:px-6 lg:px-10 2xl:px-16">
        <h2 className="max-w-2xl text-3xl font-semibold sm:text-4xl">{t("landing.featuresTitle")}</h2>
        <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((f, i) => (
            <motion.div
              key={f.key}
              initial={{ opacity: 0, y: 16 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: i * 0.06 }}
              className="rounded-2xl border border-line bg-surface p-6"
            >
              <span className="grid size-11 place-items-center rounded-xl bg-accent-soft text-accent">
                <f.icon className="size-5" />
              </span>
              <h3 className="mt-4 text-lg font-semibold">{t(`landing.features.${f.key}.title`)}</h3>
              <p className="mt-2 text-sm leading-relaxed text-muted">{t(`landing.features.${f.key}.text`)}</p>
            </motion.div>
          ))}
        </div>
      </section>
    </>
  );
}
