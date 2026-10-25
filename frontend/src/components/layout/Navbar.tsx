import { Dumbbell, LogOut, Menu, Shield, X } from "lucide-react";
import { AnimatePresence, motion } from "motion/react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, NavLink, useLocation, useNavigate } from "react-router";
import { toast } from "sonner";
import { buttonClass } from "@/components/ui/Button";
import { cn, initials } from "@/lib/utils";
import { useAuth } from "@/providers/AuthProvider";
import { LanguageSwitcher } from "./LanguageSwitcher";
import { Logo } from "./Logo";
import { ThemeToggle } from "./ThemeToggle";

const linkClass = ({ isActive }: { isActive: boolean }) =>
  cn("rounded-full px-4 py-2 text-sm font-medium transition-colors", isActive ? "bg-surface-2 text-fg" : "text-muted hover:text-fg");

export function Navbar() {
  const { t } = useTranslation();
  const { user, isPlatformAdmin, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    setOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  const handleLogout = () => {
    logout();
    toast(t("auth.loggedOut"));
    navigate("/");
  };

  const links = !user
    ? []
    : [
        { to: "/gyms", label: t("nav.myGyms"), icon: Dumbbell },
        ...(isPlatformAdmin ? [{ to: "/platform", label: t("nav.platform"), icon: Shield }] : []),
      ];

  return (
    <header
      className={cn(
        "sticky top-0 z-40 transition-all duration-300",
        scrolled ? "border-b border-line bg-bg/80 backdrop-blur-xl" : "border-b border-transparent",
      )}
    >
      <nav className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6 lg:px-10">
        <div className="flex items-center gap-8">
          <Logo />
          <div className="hidden items-center gap-1 md:flex">
            {links.map((l) => (
              <NavLink key={l.to} to={l.to} className={linkClass} end>
                {l.label}
              </NavLink>
            ))}
          </div>
        </div>

        <div className="hidden items-center gap-2 md:flex">
          <LanguageSwitcher />
          <ThemeToggle />
          {user ? (
            <>
              <span
                title={user.fullName}
                className="grid size-10 place-items-center rounded-full bg-accent-soft text-xs font-bold text-accent"
              >
                {initials(user.fullName)}
              </span>
              <button onClick={handleLogout} className={buttonClass("ghost", "sm")} aria-label={t("nav.logout")}>
                <LogOut className="size-4" />
                {t("nav.logout")}
              </button>
            </>
          ) : (
            <>
              <Link to="/login" state={{ from: location.pathname }} className={buttonClass("ghost", "sm")}>
                {t("nav.login")}
              </Link>
              <Link to="/register" state={{ from: location.pathname }} className={buttonClass("primary", "sm")}>
                {t("nav.register")}
              </Link>
            </>
          )}
        </div>

        <div className="flex items-center gap-2 md:hidden">
          <ThemeToggle />
          <button
            onClick={() => setOpen((o) => !o)}
            aria-expanded={open}
            aria-label={t("nav.menu")}
            className="grid size-10 cursor-pointer place-items-center rounded-full border border-line bg-surface"
          >
            {open ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>
        </div>
      </nav>

      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden border-t border-line bg-bg md:hidden"
          >
            <div className="space-y-1 px-4 py-4">
              {links.map((l) => (
                <NavLink
                  key={l.to}
                  to={l.to}
                  end
                  className={({ isActive }) => cn("flex items-center gap-3 rounded-xl px-4 py-3 font-medium", isActive ? "bg-surface-2" : "text-muted")}
                >
                  <l.icon className="size-4.5" />
                  {l.label}
                </NavLink>
              ))}
              <div className="flex items-center justify-between gap-3 pt-3">
                <LanguageSwitcher />
                {user ? (
                  <button onClick={handleLogout} className={buttonClass("outline", "sm")}>
                    <LogOut className="size-4" />
                    {t("nav.logout")}
                  </button>
                ) : (
                  <div className="flex gap-2">
                    <Link to="/login" state={{ from: location.pathname }} className={buttonClass("outline", "sm")}>
                      {t("nav.login")}
                    </Link>
                    <Link to="/register" state={{ from: location.pathname }} className={buttonClass("primary", "sm")}>
                      {t("nav.register")}
                    </Link>
                  </div>
                )}
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  );
}
