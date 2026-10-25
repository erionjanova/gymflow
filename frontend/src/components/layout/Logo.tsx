import { Dumbbell } from "lucide-react";
import { Link } from "react-router";
import { cn } from "@/lib/utils";

export function Logo({ className }: { className?: string }) {
  return (
    <Link to="/" className={cn("group flex items-center gap-2.5", className)}>
      <span className="grid size-9 place-items-center rounded-xl bg-fg text-bg transition-transform duration-300 group-hover:-rotate-12">
        <Dumbbell className="size-4.5" />
      </span>
      <span className="font-display text-xl font-semibold tracking-tight">GymFlow</span>
    </Link>
  );
}
