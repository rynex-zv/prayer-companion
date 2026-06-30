import { createFileRoute } from "@tanstack/react-router";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Picker } from "@/components/Picker";
import { RotateCcw } from "lucide-react";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/tasbih")({
  head: () => ({
    meta: [
      { title: "Tasbih — Pray Ad Free" },
      { name: "description", content: "Tasbih counter with presets." },
    ],
  }),
  component: TasbihPage,
});

type Preset = { id: string; name: string; repeatMode: string; items: { text: string; targetCount: number }[] };
type Snapshot = {
  count: number; currentPhrase: string; progressText: string;
  isPresetSelectionEnabled: boolean; selectedPresetId: string; presets: Preset[];
};

function TasbihPage() {
  const { data, refresh } = useSnapshot<Snapshot>("tasbih.getSnapshot");
  if (!data) return <div className="h-80 animate-pulse rounded-xl bg-muted" />;

  return (
    <div className="flex flex-col gap-3">
      <Card className="text-center">
        <div className="text-lg font-semibold text-primary">{data.currentPhrase}</div>
        <div className="text-xs text-muted-foreground">{data.progressText}</div>
      </Card>

      <Card className="flex flex-col items-center gap-4 py-8">
        <button
          onClick={() => mauiCall("tasbih.increment").then(refresh)}
          className={cn(
            "relative flex h-56 w-56 items-center justify-center rounded-full bg-[var(--gradient-primary)] text-6xl font-bold text-primary-foreground shadow-[var(--shadow-hero)] transition-transform active:scale-95",
          )}
        >
          <span className="tabular-nums">{data.count}</span>
        </button>
        <button
          onClick={() => mauiCall("tasbih.reset").then(refresh)}
          className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-4 py-2 text-sm font-medium hover:bg-muted"
        >
          <RotateCcw className="h-4 w-4" /> Reset
        </button>
      </Card>

      <Card>
        <div className="mb-2 text-sm font-semibold">Presets</div>
        <Picker
          value={data.selectedPresetId}
          onChange={(id) => mauiCall("tasbih.selectPreset", { id }).then(refresh)}
        >
          {data.presets.map((p) => (
            <option key={p.id} value={p.id} disabled={!data.isPresetSelectionEnabled && p.id !== data.selectedPresetId}>
              {p.name}
            </option>
          ))}
        </Picker>
        {!data.isPresetSelectionEnabled && (
          <p className="mt-2 text-xs text-muted-foreground">Reset to change preset.</p>
        )}
        <ul className="mt-3 space-y-1.5">
          {data.presets.find((p) => p.id === data.selectedPresetId)?.items.map((it, i) => (
            <li key={i} className="flex items-center justify-between rounded-md bg-muted/60 px-3 py-2 text-sm">
              <span>{it.text}</span>
              <span className="text-xs font-semibold tabular-nums text-muted-foreground">×{it.targetCount}</span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
