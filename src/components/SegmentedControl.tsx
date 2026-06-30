import { cn } from "@/lib/utils";

export type Option = { id: string; label: string };

export function SegmentedControl({
  value, onChange, options, className,
}: { value: string; onChange: (id: string) => void; options: Option[]; className?: string }) {
  return (
    <div className={cn("inline-flex rounded-full bg-muted p-1 text-sm", className)}>
      {options.map((o) => (
        <button
          key={o.id}
          type="button"
          onClick={() => onChange(o.id)}
          className={cn(
            "rounded-full px-3 py-1.5 font-medium transition-colors",
            value === o.id ? "bg-card text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground",
          )}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}
