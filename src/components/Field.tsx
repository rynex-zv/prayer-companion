import { cn } from "@/lib/utils";
import type { HTMLAttributes, ReactNode } from "react";

export function Field({ label, children, hint, className, ...props }: { label: string; children: ReactNode; hint?: string; className?: string } & HTMLAttributes<HTMLLabelElement>) {
  return (
    <label className={cn("flex flex-col gap-1.5", className)} {...props}>
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      {children}
      {hint ? <span className="text-xs text-muted-foreground/80">{hint}</span> : null}
    </label>
  );
}
