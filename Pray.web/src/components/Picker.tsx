import { cn } from "@/lib/utils";
import { ChevronDown } from "lucide-react";
import type { ReactNode } from "react";

export function Picker({ value, onChange, children, className, selectorName, ariaLabel }: { value: string; onChange: (v: string) => void; children: ReactNode; className?: string; selectorName?: string; ariaLabel?: string }) {
  return (
    <div className={cn("relative", className)}>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        data-selector-name={selectorName}
        aria-label={ariaLabel}
        className="w-full appearance-none rounded-lg border border-input bg-card px-3 py-2 pr-8 text-sm text-card-foreground focus:outline-none focus:ring-2 focus:ring-ring"
      >
        {children}
      </select>
      <ChevronDown className="pointer-events-none absolute right-2 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
    </div>
  );
}
