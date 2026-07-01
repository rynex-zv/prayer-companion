import { cn } from "@/lib/utils";
import type { ReactNode, HTMLAttributes } from "react";

export function Card({ className, children, ...props }: { className?: string; children: ReactNode } & HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn("glass-card p-4", className)} {...props}>
      {children}
    </div>
  );
}

export function CardTitle({ children, className }: { children: ReactNode; className?: string }) {
  return <h2 className={cn("text-sm font-semibold uppercase tracking-wider text-muted-foreground", className)}>{children}</h2>;
}
