import { useEffect, useState } from "react";

export function CoordinateInput({
  label,
  value,
  selectorName,
  onCommit,
}: {
  label: string;
  value: number;
  selectorName: string;
  onCommit: (value: number) => void;
}) {
  const [draft, setDraft] = useState(String(value));

  useEffect(() => {
    setDraft(String(value));
  }, [value]);

  const commit = () => {
    const parsed = Number(draft.trim());
    if (!Number.isFinite(parsed)) {
      setDraft(String(value));
      return;
    }

    if (parsed !== value) {
      onCommit(parsed);
    }
  };

  return (
    <label className="block text-xs text-muted-foreground">
      {label}
      <input
        value={draft}
        inputMode="decimal"
        onChange={(event) => setDraft(event.currentTarget.value)}
        onBlur={commit}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.currentTarget.blur();
          }
        }}
        data-selector-name={selectorName}
        className="mt-1 min-h-9 w-full rounded-md border border-input bg-card px-3 py-2 text-sm text-card-foreground"
        dir="ltr"
      />
    </label>
  );
}
