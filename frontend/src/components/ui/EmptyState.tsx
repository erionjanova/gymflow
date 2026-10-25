import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

export function EmptyState({ icon: Icon, text, action }: { icon: LucideIcon; text: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-4 rounded-2xl border border-dashed border-line px-6 py-14 text-center">
      <div className="grid size-14 place-items-center rounded-2xl bg-accent-soft text-accent">
        <Icon className="size-6" />
      </div>
      <p className="max-w-xs text-muted">{text}</p>
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry, retryLabel }: { message: string; onRetry: () => void; retryLabel: string }) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-danger/30 bg-danger-soft px-6 py-10 text-center">
      <p className="text-danger">{message}</p>
      <button onClick={onRetry} className="cursor-pointer text-sm font-semibold underline underline-offset-4">
        {retryLabel}
      </button>
    </div>
  );
}
