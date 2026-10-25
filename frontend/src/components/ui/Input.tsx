import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";
import { useTranslation } from "react-i18next";
import { cn } from "@/lib/utils";

const fieldBase =
  "w-full rounded-xl border border-line bg-surface px-4 text-fg placeholder:text-muted/70 transition-colors outline-none focus:border-accent focus:ring-4 focus:ring-ring aria-[invalid=true]:border-danger aria-[invalid=true]:focus:ring-danger/20";

export const Input = ({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) => (
  <input className={cn(fieldBase, "h-12", className)} {...props} />
);

export const Textarea = ({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) => (
  <textarea className={cn(fieldBase, "min-h-24 resize-y py-3", className)} {...props} />
);

export const Select = ({ className, ...props }: SelectHTMLAttributes<HTMLSelectElement>) => (
  <select className={cn(fieldBase, "h-12 cursor-pointer pr-10", className)} {...props} />
);

interface FieldProps {
  label: string;
  error?: string;
  hint?: string;
  optional?: boolean;
  children: (id: string, describedBy: string | undefined) => ReactNode;
}

export function Field({ label, error, hint, optional, children }: FieldProps) {
  const { t } = useTranslation();
  const id = useId();
  const describedBy = error || hint ? `${id}-desc` : undefined;

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="flex items-baseline gap-2 text-sm font-medium">
        {label}
        {optional && <span className="text-xs font-normal text-muted">({t("common.optional")})</span>}
      </label>
      {children(id, describedBy)}
      {(error || hint) && (
        <p id={describedBy} className={cn("text-xs", error ? "text-danger" : "text-muted")}>
          {error ? t(error as "validation.required", { defaultValue: error }) : hint}
        </p>
      )}
    </div>
  );
}
