import { ChevronDown, Dumbbell, LogOut, Shield, UserRound } from "lucide-react";
import { AnimatePresence, motion } from "motion/react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation } from "react-router";
import { cn, initials } from "@/lib/utils";
import { useAuth } from "@/providers/AuthProvider";

const itemClass = "flex w-full cursor-pointer items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors hover:bg-surface-2";

export function UserMenu({ onLogout }: { onLogout: () => void }) {
  const { t } = useTranslation();
  const { user, isPlatformAdmin } = useAuth();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!open) return;
    const onClick = (e: MouseEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  if (!user) return null;

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t("nav.account")}
        className="flex h-10 cursor-pointer items-center gap-1.5 rounded-full border border-line bg-surface py-1 pr-2.5 pl-1 transition-colors hover:bg-surface-2"
      >
        <span className="grid size-8 place-items-center rounded-full bg-accent-soft text-xs font-bold text-accent">{initials(user.fullName)}</span>
        <ChevronDown className={cn("size-4 text-muted transition-transform", open && "rotate-180")} />
      </button>

      <AnimatePresence>
        {open && (
          <motion.div
            role="menu"
            initial={{ opacity: 0, y: -6, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -6, scale: 0.98 }}
            transition={{ duration: 0.15 }}
            className="absolute right-0 z-50 mt-2 w-64 origin-top-right rounded-2xl border border-line bg-surface p-2 shadow-xl shadow-black/10"
          >
            <div className="px-3 pt-2 pb-3">
              <p className="truncate font-semibold">{user.fullName}</p>
              <p className="truncate text-xs text-muted">{user.email}</p>
            </div>
            <div className="border-t border-line pt-2">
              <Link to="/account" role="menuitem" className={itemClass}>
                <UserRound className="size-4 text-muted" />
                {t("nav.account")}
              </Link>
              <Link to="/gyms" role="menuitem" className={itemClass}>
                <Dumbbell className="size-4 text-muted" />
                {t("nav.myGyms")}
              </Link>
              {isPlatformAdmin && (
                <Link to="/platform" role="menuitem" className={itemClass}>
                  <Shield className="size-4 text-muted" />
                  {t("nav.platform")}
                </Link>
              )}
            </div>
            <div className="mt-2 border-t border-line pt-2">
              <button role="menuitem" onClick={onLogout} className={cn(itemClass, "text-danger")}>
                <LogOut className="size-4" />
                {t("nav.logout")}
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
