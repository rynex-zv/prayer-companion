import { createFileRoute } from "@tanstack/react-router";
import { type PointerEvent, useMemo, useRef, useState } from "react";
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
    meta: [],
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

type TasbihRingProps = {
  count: number;
  currentPhrase: string;
  progressText: string;
  beadCount?: number;
  onIncrement: () => void | Promise<void>;
};

type Point = {
  x: number;
  y: number;
  angle: number;
};

function normalizeAngle(angle: number) {
  return Math.atan2(Math.sin(angle), Math.cos(angle));
}

function getPageDirectionMultiplier() {
  if (typeof document === "undefined") return 1;

  const dir =
    document.documentElement.getAttribute("dir") ||
    window.getComputedStyle(document.documentElement).direction ||
    "ltr";

  return dir.toLowerCase() === "rtl" ? 1 : -1;
}

function TasbihRing({
  count,
  currentPhrase,
  progressText,
  beadCount = 33,
  onIncrement,
}: TasbihRingProps) {
  const safeBeadCount = Math.max(21, Math.min(beadCount, 33));

  const directionMultiplier = useMemo(() => getPageDirectionMultiplier(), []);

  const [dragging, setDragging] = useState(false);
  const [dragDistance, setDragDistance] = useState(0);

  const startY = useRef(0);
  const dragDistanceRef = useRef(0);
  const busyRef = useRef(false);
  const ignoreNextClick = useRef(false);

  const size = 340;
  const centerX = size / 2;
  const centerY = size / 2;
  const radius = 138;
  const dragStep = 32;
  const swipeThreshold = 22;

  const incrementOnce = async () => {
    if (busyRef.current) return;
    busyRef.current = true;
    if (typeof navigator !== "undefined" && "vibrate" in navigator) {
      try { navigator.vibrate?.(8); } catch { /* noop */ }
    }
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
    const nextDistance = Math.max(-70, Math.min(70, e.clientY - startY.current));
    dragDistanceRef.current = nextDistance;
    setDragDistance(nextDistance);
  };

  const handlePointerUp = () => {
    const shouldIncrement = Math.abs(dragDistanceRef.current) > swipeThreshold;
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

  const visualOffset =
    directionMultiplier * count + directionMultiplier * (dragDistance / dragStep);

  const points: Point[] = Array.from({ length: safeBeadCount }).map((_, i) => {
    const angle = ((i - visualOffset) / safeBeadCount) * Math.PI * 2 - Math.PI / 2;
    return {
      angle,
      x: centerX + Math.cos(angle) * radius,
      y: centerY + Math.sin(angle) * radius,
    };
  });

  let activeIndex = 0;
  let smallestTopDistance = Number.POSITIVE_INFINITY;
  points.forEach((point, index) => {
    const topDistance = Math.abs(normalizeAngle(point.angle + Math.PI / 2));
    if (topDistance < smallestTopDistance) {
      smallestTopDistance = topDistance;
      activeIndex = index;
    }
  });

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
        "relative mx-auto touch-none select-none outline-none cursor-pointer",
        dragging && "scale-[0.99] transition-transform",
      )}
      style={{ width: size, height: size }}
    >
      {/* outer soft ring */}
      <div
        className="pointer-events-none absolute rounded-full bg-gradient-to-br from-primary/8 to-transparent"
        style={{
          left: centerX - radius - 26,
          top: centerY - radius - 26,
          width: (radius + 26) * 2,
          height: (radius + 26) * 2,
        }}
      />
      {/* thin guide circle */}
      <div
        className="pointer-events-none absolute rounded-full border border-dashed border-primary/15"
        style={{
          left: centerX - radius,
          top: centerY - radius,
          width: radius * 2,
          height: radius * 2,
        }}
      />

      {/* beads — layered Islamic style: pearl highlight + primary body + inner ring */}
      {points.map((point, index) => {
        const isActive = index === activeIndex;
        const beadSize = isActive ? 26 : 16;
        return (
          <div
            key={`bead-${index}`}
            className={cn(
              "pointer-events-none absolute rounded-full",
              isActive ? "z-20" : "z-10",
            )}
            style={{
              left: point.x,
              top: point.y,
              width: beadSize,
              height: beadSize,
              transform: "translate(-50%, -50%)",
              background: isActive
                ? "radial-gradient(circle at 32% 30%, oklch(1 0 0 / 0.65) 0%, oklch(1 0 0 / 0.15) 22%, transparent 42%), radial-gradient(circle at 62% 68%, color-mix(in oklab, var(--color-primary) 100%, black 22%) 0%, var(--color-primary) 55%, color-mix(in oklab, var(--color-primary) 65%, black 25%) 100%)"
                : "radial-gradient(circle at 32% 30%, oklch(1 0 0 / 0.45) 0%, transparent 45%), radial-gradient(circle at 60% 65%, color-mix(in oklab, var(--color-primary) 70%, transparent) 0%, color-mix(in oklab, var(--color-primary) 35%, transparent) 100%)",
              boxShadow: isActive
                ? "inset 0 -2px 4px color-mix(in oklab, var(--color-primary) 60%, black 40%), inset 0 0 0 1px color-mix(in oklab, var(--color-primary) 55%, black), 0 3px 10px -3px color-mix(in oklab, var(--color-primary) 60%, transparent)"
                : "inset 0 -1px 2px color-mix(in oklab, var(--color-primary) 40%, black 20% / 30%), inset 0 0 0 1px color-mix(in oklab, var(--color-primary) 30%, transparent)",
            }}
          />
        );
      })}

      {/* center face — Islamic rosette panel */}
      <div
        className="pointer-events-none absolute z-30 flex flex-col items-center justify-center rounded-full border border-border/60 bg-card px-6 text-center shadow-[inset_0_1px_0_rgba(255,255,255,0.5),0_10px_30px_-12px_rgba(0,0,0,0.15)]"
        style={{
          left: centerX,
          top: centerY,
          width: 200,
          height: 200,
          transform: "translate(-50%, -50%)",
          backgroundImage:
            "radial-gradient(circle at 50% 50%, color-mix(in oklab, var(--color-primary) 8%, transparent) 0%, transparent 60%)",
        }}
      >
        {/* subtle 8-point star rosette */}
        <svg
          aria-hidden
          viewBox="0 0 100 100"
          className="pointer-events-none absolute inset-0 h-full w-full opacity-[0.07]"
        >
          <g fill="none" stroke="currentColor" strokeWidth="0.6" className="text-primary">
            <circle cx="50" cy="50" r="46" />
            <circle cx="50" cy="50" r="34" />
            {Array.from({ length: 8 }).map((_, i) => {
              const a = (i * Math.PI) / 4;
              return (
                <line
                  key={i}
                  x1={50 + Math.cos(a) * 12}
                  y1={50 + Math.sin(a) * 12}
                  x2={50 + Math.cos(a) * 46}
                  y2={50 + Math.sin(a) * 46}
                />
              );
            })}
            {Array.from({ length: 8 }).map((_, i) => {
              const a = (i * Math.PI) / 4 + Math.PI / 8;
              return (
                <line
                  key={`d-${i}`}
                  x1={50 + Math.cos(a) * 18}
                  y1={50 + Math.sin(a) * 18}
                  x2={50 + Math.cos(a) * 40}
                  y2={50 + Math.sin(a) * 40}
                />
              );
            })}
          </g>
        </svg>
        <div className="relative line-clamp-2 text-base font-semibold leading-snug text-primary">
          {currentPhrase}
        </div>
        <div className="relative mt-1 text-xs font-medium text-muted-foreground tabular-nums">
          {progressText}
        </div>
        <div className="relative mt-2 text-5xl font-bold tabular-nums text-foreground">
          {count}
        </div>
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
        <TasbihRing
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
