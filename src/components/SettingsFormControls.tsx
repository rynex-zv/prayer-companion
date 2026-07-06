import { cn } from "@/lib/utils";
import type { ReactNode } from "react";

export function StatusLine({ value, selectorName }: { value: string; selectorName: string }) {
  return (
    <div
      data-selector-name={selectorName}
      className="rounded-md border border-border bg-card p-3 text-sm text-card-foreground"
    >
      {value}
    </div>
  );
}

export function EditableSetting({
  label,
  value,
  selectorName,
  onChange,
  className,
}: {
  label: string;
  value: string | number;
  selectorName: string;
  onChange: (value: string) => void;
  className?: string;
}) {
  return (
    <div className={cn("text-sm text-card-foreground", className)}>
      <span className="mb-1 block text-xs text-muted-foreground">{label}</span>
      <div
        contentEditable
        suppressContentEditableWarning
        onInput={(event) => onChange(event.currentTarget.textContent ?? "")}
        data-selector-name={selectorName}
        className="min-h-9 w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-card-foreground"
      >
        {String(value)}
      </div>
    </div>
  );
}

export function ToggleSetting({
  label,
  checked,
  onChange,
  selectorName,
  onLabel,
  offLabel,
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  selectorName: string;
  onLabel: string;
  offLabel: string;
}) {
  return (
    <button
      type="button"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      data-selector-name={selectorName}
      className="flex items-center justify-between gap-3 rounded-md border border-border bg-card px-3 py-2 text-start text-sm text-card-foreground"
    >
      <span>{label}</span>
      <span className="rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">
        {checked ? onLabel : offLabel}
      </span>
    </button>
  );
}

export function OptionButtons({
  label,
  value,
  options,
  selectorName,
  onChange,
}: {
  label: string;
  value: string;
  options: { id: string; label: string }[];
  selectorName: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="rounded-md border border-border bg-card p-3 text-sm text-card-foreground">
      <div className="mb-2 text-xs font-medium text-muted-foreground">{label}</div>
      <div className="flex flex-wrap gap-2">
        {options.map((option) => (
          <button
            key={option.id}
            type="button"
            aria-checked={value === option.id}
            onClick={() => onChange(option.id)}
            data-selector-name={`${selectorName}:${option.id}`}
            className={cn(
              "rounded-md border px-3 py-1.5 text-xs font-medium",
              value === option.id
                ? "border-primary bg-primary text-primary-foreground"
                : "border-border bg-card text-card-foreground",
            )}
          >
            {option.label}
          </button>
        ))}
      </div>
    </div>
  );
}

export function SectionBlock({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-md border border-border bg-card p-3">
      <h2 className="mb-3 text-sm font-semibold text-card-foreground">{title}</h2>
      <div className="flex flex-col gap-3">{children}</div>
    </section>
  );
}
