import { cn } from "@/lib/utils";
import { useAppStore } from "@/state/appStore";

export type Option = { id: string; label: string };

export function SegmentedControl({
  value, onChange, options, className, selectorPrefix,
}: { value: string; onChange: (id: string) => void; options: Option[]; className?: string; selectorPrefix?: string }) {
  const direction = useAppStore((state) => state.direction);
  const orderedOptions = direction === "rtl" ? [...options].reverse() : options;
  return (
    <div className={cn("inline-flex rounded-full bg-muted p-1 text-sm", className)} dir={direction}>
      {orderedOptions.map((o) => (
        <button
          key={o.id}
          type="button"
          onClick={() => onChange(o.id)}
          data-selector-name={selectorPrefix ? `${selectorPrefix}:${o.id}` : undefined}
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
