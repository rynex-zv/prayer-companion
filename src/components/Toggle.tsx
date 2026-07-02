import { cn } from "@/lib/utils";

export function Toggle({ checked, onChange, label, className, selectorName }: { checked: boolean; onChange: (v: boolean) => void; label?: string; className?: string; selectorName?: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      data-selector-name={selectorName}
      className={cn("inline-flex items-center gap-3 text-card-foreground", className)}
    >
      <span className={cn("relative inline-block h-6 w-11 rounded-full transition-colors", checked ? "bg-primary" : "bg-muted")}>
        <span className={cn("absolute top-0.5 h-5 w-5 rounded-full bg-card shadow transition-all", checked ? "left-[22px]" : "left-0.5")} />
      </span>
      {label ? <span className="text-sm">{label}</span> : null}
    </button>
  );
}
