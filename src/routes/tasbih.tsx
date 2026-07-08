import { createFileRoute } from "@tanstack/react-router";
import { type PointerEvent, useRef, useState } from "react";
import { useSnapshot } from "@/hooks/useSnapshot";
import { mauiCall } from "@/native/mauiWebberClient";
import { Card } from "@/components/Card";
import { Picker } from "@/components/Picker";
import { RotateCcw } from "lucide-react";
import { cn } from "@/lib/utils";
import { PageLog } from "@/components/PageLog";
import { usePageLog } from "@/hooks/usePageLog";
import { useAppLabels } from "@/hooks/useAppLabels";

export const Route = createFileRoute("/tasbih")({
  head: () => ({
    meta: [
      { title: "Tasbih — Pray Ad Free" },
      { name: "description", content: "Tasbih counter with presets." },
    ],
  }),
  component: TasbihPage,
});

type Preset = {
  id: string;
  name: string;
  repeatMode: string;
  items: {
    text: string;
    targetCount: number;
  }[];
};

type Snapshot = {
  count: number;
  currentPhrase: string;
  progressText: string;
  isPresetSelectionEnabled: boolean;
  selectedPresetId: string;
  presets: Preset[];
};

type MovingTasbihProps = {
  count: number;
  currentPhrase: string;
  progressText: string;
  beadCount?: number;
  onIncrement: () => void | Promise<void>;
};

function MovingTasbih({
  count,
  currentPhrase,
  progressText,
  beadCount = 33,
  onIncrement,
}: MovingTasbihProps) {
  const safeBeadCount = Math.max(11, Math.min(beadCount, 33));

  const [dragging, setDragging] = useState(false);
  const [dragDistance, setDragDistance] = useState(0);

  const startY = useRef(0);
  const dragDistanceRef = useRef(0);
  const busyRef = useRef(false);
  const ignoreNextClick = useRef(false);

  const incrementOnce = async () => {
    if (busyRef.current) return;

    busyRef.current = true;
    try {
      await onIncrement();
    } finally {
      busyRef.current = false;
    }
  };

  const handlePointerDown = (e: PointerEvent<HTMLDivElement>) => {
    e.currentTarget.setPointerCapture(e.pointerId);

    startY.current = e.clientY;
    dragDistanceRef.current = 0;
    ignoreNextClick.current = false;

    setDragging(true);
    setDragDistance(0);
  };

  const handlePointerMove = (e: PointerEvent<HTMLDivElement>) => {
    if (!dragging) return;

    const distance = Math.max(-24, Math.min(90, e.clientY - startY.current));

    dragDistanceRef.current = distance;
    setDragDistance(distance);
  };

  const handlePointerUp = () => {
    const shouldIncrement = dragDistanceRef.current > 28;

    setDragging(false);
    setDragDistance(0);
    dragDistanceRef.current = 0;

    if (shouldIncrement) {
      ignoreNextClick.current = true;
      void incrementOnce();
    }
  };

  const handleClick = () => {
    if (ignoreNextClick.current) {
      ignoreNextClick.current = false;
      return;
    }

    void incrementOnce();
  };

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={handleClick}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          void incrementOnce();
        }
      }}
      className={cn(
        "relative h-[430px] w-full max-w-[330px] touch-none select-none outline-none",
        "cursor-pointer rounded-[2rem]",
      )}
      aria-label="Tasbih counter"
    >
      <div className="absolute inset-x-0 top-0 mx-auto h-[390px] w-[280px]">
        {/* string */}
        <div className="absolute left-1/2 top-1/2 h-[340px] w-[170px] -translate-x-1/2 -translate-y-1/2 rounded-full border border-border/70" />

        {/* fixed opening / thumb gate */}
        <div className="absolute bottom-[23px] left-1/2 z-20 flex h-16 w-28 -translate-x-1/2 items-center justify-center rounded-full bg-card">
          <div className="h-12 w-20 rounded-full border border-dashed border-primary/45 bg-muted/40" />
        </div>

        {/* center text */}
        <div className="absolute left-1/2 top-1/2 z-10 flex h-40 w-40 -translate-x-1/2 -translate-y-1/2 flex-col items-center justify-center rounded-full border border-border bg-background/90 px-4 text-center shadow-sm backdrop-blur">
          <div className="line-clamp-2 text-base font-bold leading-snug text-primary">
            {currentPhrase}
          </div>

          <div className="mt-1 text-xs text-muted-foreground">
            {progressText}
          </div>

          <div className="mt-3 text-5xl font-bold tabular-nums text-primary">
            {count}
          </div>
        </div>

        {/* beads */}
        {Array.from({ length: safeBeadCount }).map((_, i) => {
          /**
           * The beads rotate around the oval.
           * The fixed gap stays in the same place at the bottom.
           */
          const visualCount = count + dragDistance / 28;
          const angle =
            ((i - visualCount) / safeBeadCount) * Math.PI * 2 - Math.PI / 2;

          const x = 140 + Math.cos(angle) * 84;
          const y = 195 + Math.sin(angle) * 168;

          const bottomDistance = Math.abs(angle - Math.PI / 2);
          const isInsideFixedOpening = bottomDistance < 0.22;

          const isNearFinger =
            Math.abs(Math.sin(angle) - 1) < 0.18 && Math.cos(angle) < 0.45;

          return (
            <div
              key={i}
              className={cn(
                "absolute z-10 rounded-full border shadow-sm transition-[width,height,opacity,box-shadow]",
                "h-8 w-8 border-primary/25 bg-[radial-gradient(circle_at_30%_30%,hsl(var(--primary-foreground)),hsl(var(--primary))_45%,hsl(var(--primary)/0.75))]",
                isNearFinger && "h-9 w-9 shadow-md",
                isInsideFixedOpening && "opacity-20",
              )}
              style={{
                left: x,
                top: y,
                transform: "translate(-50%, -50%)",
              }}
            />
          );
        })}

        {/* separator bead */}
        <div
          className="absolute left-1/2 top-[16px] z-20 h-12 w-12 -translate-x-1/2 rounded-full border border-primary/40 bg-card shadow-md"
          style={{
            transform: `translateX(-50%) translateY(${dragging ? dragDistance * 0.12 : 0}px)`,
          }}
        >
          <div className="absolute left-1/2 top-1/2 h-7 w-7 -translate-x-1/2 -translate-y-1/2 rounded-full bg-primary/25" />
        </div>
      </div>

      <div className="absolute bottom-0 left-1/2 z-30 -translate-x-1/2 rounded-full border border-border bg-background/95 px-4 py-2 text-center text-xs text-muted-foreground shadow-sm">
        اضغط أو اسحب المسبحة للأسفل
      </div>
    </div>
  );
}

function TasbihPage() {
  usePageLog("tasbih");

  const t = useAppLabels();
  const { data, refresh } = useSnapshot<Snapshot>("tasbih.getSnapshot");

  if (!data) {
    return <div className="h-80 animate-pulse rounded-xl bg-muted" />;
  }

  const selectedPreset = data.presets.find(
    (p) => p.id === data.selectedPresetId,
  );

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-end">
        <PageLog page="tasbih" />
      </div>

      <Card className="flex flex-col items-center gap-4 py-6">
        <MovingTasbih
          count={data.count}
          currentPhrase={data.currentPhrase}
          progressText={data.progressText}
          beadCount={33}
          onIncrement={() => mauiCall("tasbih.increment").then(refresh)}
        />

        <button
          onClick={() => mauiCall("tasbih.reset").then(refresh)}
          className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-4 py-2 text-sm font-medium hover:bg-muted"
        >
          <RotateCcw className="h-4 w-4" />
          {t("reset")}
        </button>
      </Card>

      <Card>
        <div className="mb-2 text-sm font-semibold">{t("presets")}</div>

        <Picker
          value={data.selectedPresetId}
          onChange={(id) =>
            mauiCall("tasbih.selectPreset", { id }).then(refresh)
          }
        >
          {data.presets.map((p) => (
            <option
              key={p.id}
              value={p.id}
              disabled={
                !data.isPresetSelectionEnabled &&
                p.id !== data.selectedPresetId
              }
            >
              {p.name}
            </option>
          ))}
        </Picker>

        {!data.isPresetSelectionEnabled && (
          <p className="mt-2 text-xs text-muted-foreground">
            {t("resetToChangePreset")}
          </p>
        )}

        <ul className="mt-3 space-y-1.5">
          {selectedPreset?.items.map((it, i) => (
            <li
              key={i}
              className="flex items-center justify-between rounded-md bg-muted/60 px-3 py-2 text-sm"
            >
              <span>{it.text}</span>
              <span className="text-xs font-semibold tabular-nums text-muted-foreground">
                ×{it.targetCount}
              </span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}