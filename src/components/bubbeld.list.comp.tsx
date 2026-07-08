import { cn } from "@/lib/utils";

type Option = { id: string; label: string };

type SingleProps = {
  label?: string;
  value: string;
  options: Option[];
  selectorName: string;
  onChange: (value: string) => void;
  multiple?: false;
};

type MultiProps = {
  label?: string;
  value: string[];
  options: Option[];
  selectorName: string;
  onChange: (value: string[]) => void;
  multiple: true;
};

type BubbeldListCompProps = SingleProps | MultiProps;

export function BubbeldListComp(props: BubbeldListCompProps) {
  const selected = new Set(Array.isArray(props.value) ? props.value : [props.value]);

  const toggle = (id: string) => {
    if (!props.multiple) {
      props.onChange(id);
      return;
    }

    const next = new Set(selected);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }

    props.onChange(Array.from(next));
  };

  return (
    <div className="rounded-md border border-border bg-card p-3 text-sm text-card-foreground">
      {props.label ? <div className="mb-2 text-xs font-medium text-muted-foreground">{props.label}</div> : null}
      <div className="flex flex-wrap gap-2">
        {props.options.map((option) => {
          const isSelected = selected.has(option.id);
          return (
            <button
              key={option.id}
              type="button"
              aria-checked={isSelected}
              onClick={() => toggle(option.id)}
              data-selector-name={`${props.selectorName}:${option.id}`}
              className={cn(
                "rounded-md border px-3 py-1.5 text-xs font-medium",
                isSelected
                  ? "border-primary bg-primary text-primary-foreground"
                  : "border-border bg-card text-card-foreground",
              )}
            >
              {option.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
